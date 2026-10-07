# TASK-212A — Card & Relic Content Decision Contract (AUDIT)

```text
Task ID:            TASK-212A
Type:               AUDIT / DECISION (evidence-based contract audit; no gameplay,
                    API, frontend, backend, SignalR, documentation, or contract
                    change)
Status:             DONE (audit and decision recorded; see "What DONE means here")
Risk:               NONE (read-only audit; zero production/test/docs file modified)
Priority:           HIGH (determines the next implementation task)
Primary Agent:      review / product-audit
Parent:             TASK-212 (SPLIT decision) → this audit verifies its A-03 half
Evidence base:      current working tree (src/, tests/, docs/, tasks/)
Model:              DeepSeek Harness agent
```

**Scope discipline.** This task audits and decides only. No production code, test,
migration, contract, ADR, or `docs/` file was modified. The single file created is
this record. Open decisions recorded elsewhere (TASK-212B, TASK-213, TASK-215,
TASK-217, TASK-218, TASK-219) are **not** resolved here.

**What DONE means here.** This record contains a *decision*, and a decision is
only implemented by a follow-up task. Nothing in §6/§7 is implemented by
TASK-212A.

**No implementation is performed here.** The task proposed in §7 is a proposal
with a reserved ID, not an authorization.

---

## 0. Verification Performed (audit evidence base)

```text
git status --porcelain       26 modified tracked files + the 7 pre-existing
                             untracked task records (TASK-207/208/208A/210/211/
                             212 deliverables) + this audit's file. The 26
                             tracked paths and the 7 pre-existing records are
                             the same before and after this audit: no tracked
                             file was touched, and no pre-existing untracked file
                             was touched.
git diff --check             PASS (exit 0)
New file check               the audit record itself: 0 trailing-whitespace
                             lines, 0 tab indents, LF line endings (its untracked
                             status puts it outside `git diff --check`'s scope,
                             so it was checked directly)
Read-only inspections        (a) this audit's direct reads of every file listed in
                             §9; (b) two independent read-only inspections
                             performed within this audit, whose findings are
                             cited as evidence: a frontend Lobby/Collection
                             presentation inspection and a Card/Relic/loadout
                             test inventory
Client test suite (subset)   npx vitest run — the four Card/Relic/collection
                             suites (CollectionService 27, LobbyScene 83,
                             CollectionViewerScene 55, RuntimeBoundaries 54) —
                             PASS: 4 files / 219 tests at this tree (see §9.4)
Backend suites               NOT re-run: no backend file was changed by this
                             audit or by TASK-211, and TASK-212's record reports
                             2 919 backend tests green at this tree. Every
                             backend claim below is verified by direct source and
                             test reading (AGENTS.md §22), not by execution.
Live browser E2E             NOT run: this audit changes nothing observable, and
                             TASK-212's record already carries a 132-check × 2-run
                             live E2E result at this tree (§9.5). The E2E's
                             player-facing Lobby observations are cited, not
                             re-derived.
```

**Limitations.** (a) The decision in §6 is a *contract* decision made against the
current tree; it does not audit TASK-212B's realtime cost question beyond
recording the boundary. (b) Presentation wording/typography is deliberately not
decided here — only the data the presentation may use and the rules it must obey.
(c) `TestProvisionedContent.cs` seeds Pet Skill Card values (Tidal Barrier 30/30,
Iron Fang 150/+30) that differ from the Domain/provisioning values (20/20, 120/+10);
the file documents itself as fixture content, and §9.6 records it as an
observation, not a content source.

---

## 1. Executive Summary

**Current state.**

1. **`GET /api/cards` carries three members — `cardId`, `name`, `category` — and
   `GET /api/relics` carries two — `relicId`, `name`.** Both exclusion lists are
   binding, structural, and test-pinned: `CollectionResponses.cs:98-111,133-145`
   (dedicated response records whose member sets cannot grow by accident),
   `CardCollectionItem.cs:37-40` / `RelicCollectionItem.cs:37-39` (the
   Application read models), and the exclusions asserted at any depth by
   `CollectionEndpointTests.cs:662-703` (cards) and `:811-851` (relics).
   `API_CONTRACTS.md:783-786,809-811` names every withheld value explicitly,
   including `effectDefinition` for Cards and `Trigger`/`Condition`/
   `EffectDefinition` for Relics.
2. **The data the player needs already exists, is already authored, and is
   already read by the server.** `CardDefinition.EffectDefinition` is a NOT NULL
   structured array whose values `CARD_RULES.md` §2/§4.1 owns
   (`CardDefinition.cs:99-113`), and `RelicDefinition.Trigger` / `Condition` /
   `EffectDefinition` are the structured, §8-contracted members whose values
   `RELIC_RULES.md` §6/§8.5 owns (`RelicDefinition.cs:105-152`). The repository
   reads **do** load them: `CardRepository.ListUnlockedAsync` joins
   `PlayerUnlockedCard → CardDefinition` (`CardRepository.cs:47-64`) and
   `RelicRepository.ListDefinitionsAsync` returns the whole `RelicDefinition` row
   (`RelicRepository.cs:89-104`). `CollectionQueryService` then deliberately
   projects only the exposed members onto its read models
   (`CollectionQueryService.cs:192-222,240-298`). **The gap is a projection
   decision, not missing data: no column, query, join, index, or migration is
   needed.**
3. **The client provably cannot describe anything.** The Lobby's Card row is
   `` `● ${card.name}  [${card.category}]` `` plus a slot suffix
   (`LobbyScene.ts:1255-1256`) and its Relic row is `` `● ${relic.name}` ``
   (`LobbyScene.ts:1281-1284`). A Card row's only interaction is a selection
   toggle (`:1258`, `:1332-1333`); the scene contains **no** tooltip, hover
   handler, or detail panel, and **no** occurrence of `powerCost`, `description`,
   `effect`, `cost`, `trigger`, or `condition` outside comments. The Collection
   viewer's Card panel prints `Card: <name>` / `Category:` / `Card ID: <raw id>`
   (`CollectionViewerScene.ts:765-774`) and its Relic panel prints `Relic:` /
   `Relic ID:` (`:776-779`). `CollectionModels.ts:20-26` states the member lists
   are "binding and exhaustive" and forbids adding `powerCost`, `loadoutCopyLimit`
   or `effectDefinition` — so "what does this do?" is not merely unrendered, it is
   **unrepresentable** on the client today.
4. **No document authorizes a client-side substitute.** `RELIC_RULES.md` §8.2
   item 1 forbids deriving a Relic's effect from its `Name`, from
   `RelicDefinitionId` mapping, or from hardcoded per-Relic logic; no Card/Relic
   prose survives the TASK-108/TASK-131 structured supersession
   (`DATABASE.md:648-663,716-752`); and a client-side content catalog would be a
   second source of truth (`AGENTS.md` §7/§9). A Name-only Lobby is therefore the
   documented maximum, and the names are demonstrably insufficient: "Power
   Charge", "Berserker Core", "Mana Crystal", "Assassin Eye" state no effect.
5. **The requirement is real but narrower than TASK-207's wording.** The
   comprehension requirement is `GDD.md:303-306` ("The player should always
   understand … what each Card/Relic changes") plus `GDD.md:61-62` pillar 2
   (Build Diversity) and the `GDD.md:74` loop step "Equip Cards → Equip Relics".
   `MVP_SCOPE.md` §1 lists the content as IN (`:59-70`) but states **no**
   client-surface or comprehension requirement — and by its own §4 (`:143-150`)
   MVP_SCOPE cannot be the source of one. The *information* the requirement needs
   is not enumerated in GDD; it is enumerated by the domain rules, which is
   exactly why the minimum contract widening is the definition's own structured
   content and not a new authored description (§5.2).
6. **TASK-212A's scope boundary holds, and it matters here.** The Card Cost the
   loadout screen also arguably wants is a *different* value from the authored
   `PowerCost` (`CARD_RULES.md` §3.6: `EffectiveCardCost =
   truncate(PowerCost × (100 − TotalReduction)/100)` over an undelivered modifier
   collection), and `SIGNALR_PROTOCOL.md` §4 item 15 (`:1503-1528`) forbids the
   client to reconstruct a Card-cost modifier "from a Card's definition". That
   makes cost a realtime-projection decision — TASK-212B — and it is **excluded**
   from this decision (§6.4). One cross-reference must be carried forward rather
   than resolved: `SIGNALR_PROTOCOL.md:1099` says "A client that must show the
   spent Cost reads the Card's definition", which contemplates a definition-side
   cost read that TASK-212B must reconcile with §4 item 15.

**Verdict: TASK-212A is still justified, and it is correctly scoped — but the
contract half is smaller and more mechanical than TASK-212 assumed.** The
decision is **AMEND BOTH** read contracts (§6), additively, by exposing the
definition's own already-authored structured content and adding **no** new
vocabulary, **no** authored prose, **no** cost, and **no** realtime member. The
Signature Skill (A-01/TASK-219) does not block it (§6.6, §8).

---

## 2. Card Contract Findings

### 2.1 What the server exposes today

```text
GET /api/cards   →   200 [ { "cardId": "card-heal", "name": "Heal",
                             "category": "Basic" }, … ]
                     API_CONTRACTS.md:761-786
                     CollectionResponses.cs:98-111 (CardResponse)
                     CollectionQueryService.cs:192-222 (ListCardsAsync)
```

```text
member    wire type   source of truth                          exposed?
cardId    string      CardDefinition.CardDefinitionId          YES
name      string      CardDefinition.Name                      YES
category  string      CardDefinition.Category — "Basic" |      YES
                      "PetSkill" (CARD_RULES.md §1; DATABASE §3)
playerId  string      CardDefinition/ownership                 NO (deliberate)
powerCost int         CardDefinition.PowerCost                 NO (deliberate)
loadout   int         CardDefinition.LoadoutCopyLimit          NO (deliberate)
  CopyLimit
effect    array       CardDefinition.EffectDefinition          NO (deliberate)
  Definition          (structured: effectType/valueType/value/
                       duration/scope)
```

**Membership *is* the unlock state.** There is no `unlocked` member and no
`Card` entity (ADR-012 item 9); a `PetSkill` category value exists in the
contract but a `PetSkill` row is never an unlock row, so it is never returned
(`CARD_RULES.md` §1 item 4, `API_CONTRACTS.md:783-786`).

**Everything withheld is real, authored, and test-verified as withheld:**

```text
CollectionEndpointTests.cs:552-579   exactly { cardId, category, name }
CollectionEndpointTests.cs:584-605   both category wire values, member of the
                                     closed set { "Basic", "PetSkill" }
CollectionEndpointTests.cs:607-639   membership = the unlocked set only
CollectionEndpointTests.cs:662-703   forbids, by name: playerId, powerCost,
                                     loadoutCopyLimit, effectDefinition,
                                     unlocked + every §5.6 equip member — while
                                     asserting the stored definition really
                                     carries PowerCost=25, LoadoutCopyLimit=3
                                     and the effect array
CollectionQueryServiceTests.cs:378-465  the same member set and the same
                                     exclusions asserted on the Application read
                                     model (reflection over CardCollectionItem)
CollectionService.test.ts:330-350    the same on the client model, including
                                     not.toHaveProperty('effectDefinition')
```

**The authored content that exists but is not exposed** (values owned by
`CARD_RULES.md` §2/§4.1; transcribed, per `DATABASE.md:675-680`):

```text
card-heal          [ { effectType: Heal,    valueType: PercentMaxHp, value: 20 } ]
card-shield        [ { effectType: Shield,  valueType: PercentMaxHp, value: 20 } ]
card-power-charge  [ { effectType: Power,   valueType: Flat,         value: 25 } ]
card-inferno       [ { Damage, Flat, 100 }, { Burn, Flat, 50, duration: 2 } ]
card-tidal-barrier [ { Heal, PercentMaxHp, 20 }, { Shield, PercentMaxHp, 20 } ]
card-iron-fang     [ { Damage, Flat, 120 }, { Crit, PercentagePoints, 10,
                                             scope: "NextAttack" } ]
```

Pinned by `CardEffectDefinitionTests.cs:107-165,319-376` and
`CardEffectDefinitionsTests.cs:487-522,568-615`; the payload member names are
`DATABASE.md` §1's (`CardEffectDefinition.cs:112-192`), and the present-iff
conditions are §3's: `value` present iff the `valueType` interprets one,
`duration` present iff `Burn`, `scope` present iff `Crit` (`DATABASE.md:577-613`).

### 2.2 What is missing from the read contract

```text
MISSING (and required for comprehension)   the Card's structured effect array
                                           — CardDefinition.EffectDefinition
MISSING (not required for comprehension)   nothing else
DELIBERATELY NOT NEEDED                    playerId  (ownership, not content)
                                           powerCost (TASK-212B — §1.6, §6.4)
                                           loadoutCopyLimit (see §2.4)
```

### 2.3 What is already available to the client

```text
available   cardId, name, category      (CollectionModels.ts:110-124)
available   the four port capabilities  getPets / getPet / getCards / getRelics
                                        (GameRuntimeEvents.ts:520-538;
                                         GameRuntime.ts:800-802,812-814;
                                         ApiService.ts:297-299,310-312)
NOT available, anywhere in src/          any Card effect, cost, description,
                                         rarity, or content catalog — no
                                         content/data module exists under
                                         src/frontend/client/src/game/**
```

### 2.4 What can safely be displayed without a contract change

```text
TODAY       cardId (a raw machine id — A-13/A-18 territory), name, category
            and the selection slot number. That is all.
IMPROVABLE  The Lobby's Step-5 review block prints raw selected ids
WITHOUT A   (`LobbyScene.ts:1077-1084`) although `ownedCards` already holds the
CONTRACT    names; the same is true of the Pet line (`:1066`). That is an
CHANGE      implementation-only presentation defect (TASK-212 §A-18 family),
            NOT part of this decision, and it does not deliver comprehension.
NOT         No effect, trigger, condition, magnitude, or description can be
POSSIBLE    displayed from the current contract, and no client-side substitute
            is authorized (§1.4).
```

`loadoutCopyLimit` is **explicitly considered and excluded**: it is a
loadout-validity bound, not content (`CARD_RULES.md` §1 items 1-5 make it a
constraint on the *submitted* loadout), every provisioned row's value is `1`
(`CARD_RULES.md` §1 item 5), and the Lobby's selection model cannot even express
a duplicate because it toggles by `indexOf` on a distinct id
(`LobbyScene.ts:695-714`). Exposing it would widen §5.3 into loadout legality
without changing what any player can do or understand.

### 2.5 Recommendation

**AMEND** `API_CONTRACTS.md` §5.3 by adding one member — the Card's own
structured effect array — and by removing `effectDefinition` from the
not-exposed list. Field definition: §6.2.

---

## 3. Relic Contract Findings

### 3.1 What the server exposes today

```text
GET /api/relics  →   200 [ { "relicId": "relic-instance-1",
                             "name": "Berserker Core" }, … ]
                     API_CONTRACTS.md:788-811
                     CollectionResponses.cs:133-145 (RelicResponse)
                     CollectionQueryService.cs:240-298 (ListRelicsAsync)
```

```text
member     wire type  source of truth                           exposed?
relicId    string     Relic.RelicInstanceId (the owned            YES
                      INSTANCE; RELIC_RULES.md §2.2)
name       string     RelicDefinition.Name (through the           YES
                      instance's definition reference)
playerId   string     Relic.PlayerId                              NO (deliberate)
acquiredAt string     Relic.AcquiredAt                            NO (deliberate)
definitionId string   Relic.RelicDefinitionId                     NO (deliberate)
Trigger    string     RelicDefinition.Trigger                     NO (deliberate)
Condition  object     RelicDefinition.Condition (structured,      NO (deliberate)
                      nullable)
Effect     array      RelicDefinition.EffectDefinition            NO (deliberate)
  Definition
```

**The instance → definition mapping already works and is already used.** §5.4's
`name` is the definition's name resolved through the instance
(`API_CONTRACTS.md:802-806`; `CollectionQueryService.cs:258-295`). Two owned
instances of one definition are two elements with distinct `relicId`s and the
same `name` (`CollectionEndpointTests.cs:757-782`).
This is the TASK-212 correction to TASK-210: the Relic *name* gap is
implementation-only (TASK-212 §A-07), and this audit confirms the same
projection can carry content with no new query.

**Everything withheld is real, authored, and test-verified as withheld:**

```text
CollectionEndpointTests.cs:730-755   exactly { name, relicId }
CollectionEndpointTests.cs:811-851   forbids, by name: playerId, acquiredAt,
                                     definitionId, relicDefinitionId, trigger,
                                     condition, effect, effectDefinition,
                                     + every §5.6 equip member — and asserts the
                                     withheld values do not leak at ANY depth
                                     (Assert.DoesNotContain on the raw body)
CollectionQueryServiceTests.cs:510-603  the same member set and exclusions on
                                     the Application read model; the stored
                                     definition is asserted to really carry
                                     Trigger = "OnCombo3Plus", the Condition and
                                     the effect array
CollectionService.test.ts:398-432    the same on the client model
RelicWireProjectionTests.cs:46-74    the in-battle RelicTriggered wire shape stays
                                     { type, relicId } — no effect summary, and
                                     it is asserted absent
```

**The authored content that exists but is not exposed** (values owned by
`RELIC_RULES.md` §6 and §8.5; structures owned by §8.1-§8.3):

```text
Berserker Core   OnMatchCount   MatchCountAtLeast(3)   [ ATK  Percentage 5  Pet Battle ]
Mana Crystal     OnMatchCount   MatchCountAtLeast(4)   [ Power Flat 10 Pet Immediate ]
Assassin Eye     OnCombo        ComboAtLeast(3)        [ Crit PercentagePoints 10 Pet NextAttack ]
Emergency Core   OnHpBelow      HpPercentageBelow(30)  [ CardCost Percentage 50 Pet Battle ]
Burning Curse    OnBattleStart  —                      [ BurnDamage Percentage 30 Pet Battle ]
Combo Fang       OnCombo        ComboAtLeast(5)        [ Crit PercentagePoints 20 Pet NextAttack ]
Arcane Battery   OnPowerGain    —                      [ Power Flat 5 Pet Immediate ]
Execution Mark   OnHpBelow      HpPercentageBelow(30)  [ Crit PercentagePoints 15 Pet NextAttack ]
Cascade Core     OnCascade      —                      [ Power Flat 5 Pet Immediate ]
Battle Instinct  OnDamageTaken  —                      [ ATK Percentage 10 Pet NextAttack ]
```

Pinned by `RelicStructuredContractTests.cs:259-341`,
`RelicProvisionedDefinitions.cs:48-120`, `RelicProvisionedContent.cs:75-227`
(all ten provisioned rows), and the provisioning tests
`PetCardRelicDefinitionProvisioningTests.cs:545-603` /
`RemainingMvpRelicDefinitionProvisioningTests.cs:304-398`.

### 3.2 What is missing from the read contract

```text
MISSING (required for comprehension)   trigger (string)
                                       condition (object | null)
                                       effectDefinition (array)
MISSING (NOT required)                 definitionId — see §3.4
MISSING (NOT required)                 Reset/Cooldown — there is nothing to
                                       expose: RELIC_RULES.md §1 declares it, but
                                       DATABASE.md §1 stores no column for it and
                                       §8.4 item 4 adds "no cooldown, no charge,
                                       and no per-Relic reset state". The
                                       re-fire default is a uniform rule, not
                                       per-definition data.
```

### 3.3 What is already available to the client

```text
available   relicId (owned instance), name
available   getRelics() on the runtime port (GameRuntimeEvents.ts:538)
NOT         which Relic is equipped (battle-scoped and unpersisted — API
available   CONTRACTS §5.6; the Lobby holds the selection itself), and
            anything about what the Relic does
```

### 3.4 What requires an API contract amendment

Only the three content members above. **`definitionId` must NOT be added**: the
response is instance-addressed and already resolves the definition for `name`
(§3.1); carrying the content inline per instance follows that precedent exactly,
and exposing the definition identity would invite a client-side
definition→content catalog — a second source of truth (`AGENTS.md` §7,
`GAME_STATE.md` §0 item 5's "no second representation"). Duplicate content for
two instances of one definition is the same, already-accepted shape as duplicate
`name` values today.

### 3.5 Recommendation

**AMEND** `API_CONTRACTS.md` §5.4 by adding three members and rewriting the
not-exposed sentence. Field definitions: §6.3.

---

## 4. Loadout UX Findings

### 4.1 What the Lobby actually is

`LobbyScene` is the **single** loadout decision point: step 1 Pet, step 2 Cards,
step 3 Relics, step 4 Boss, `START BATTLE` (`LobbyScene.ts:1054-1057`, `:1195-1215`),
and it is the screen the post-result `PLAY AGAIN` continuation returns to with the
preserved, still-editable loadout (ADR-022; `LobbyScene.ts:640-672`). The
Collection viewer is **not** reachable from it (`goBack()` starts
`MainMenuScene` only, `LobbyScene.ts:624-633`), so a Lobby-only rendering
satisfies the decision point, and the Collection panel is a second, independent
surface that must not contradict it.

### 4.2 What the player sees today (verbatim)

```text
Lobby, step 2 (cards)   `● Heal  [Basic]  slot 1`            LobbyScene.ts:1255-1256
Lobby, step 3 (relics)  `● Berserker Core  slot 1`           LobbyScene.ts:1281-1284
Lobby, step 1 (pets)    `● Xích Lang  Fire  Lv 1`            LobbyScene.ts:1231
Lobby, step 4 (bosses)  `● Kim Lôi Vương  Kim`               LobbyScene.ts:1308
Lobby, step 5 (review)  `Cards: card-heal, card-shield, …`
                        `Relics: relic-instance-1, …`        LobbyScene.ts:1077-1084
Collection, Cards panel `Card: Shield` / `Category: Basic` /
                        `Card ID: card-shield`               CollectionViewerScene.ts:765-774
Collection, Relics panel `Relic: Mana Crystal` /
                        `Relic ID: relic-instance-2`         CollectionViewerScene.ts:776-779
```

### 4.3 What the player can and cannot understand

```text
CAN   which content exists (names), which is selected, the slot order, the
      Card's category, and the Pet's name/element/level
CANNOT
  - what any Basic Card changes      ("Heal" does not state 20% Max HP; "Power
                                      Charge" does not state +25 Power)
  - what any Relic does, or when     ("Berserker Core" states neither +5% ATK
                                      nor "after 3 cumulative Matches")
  - what a Pet Skill card does       (not returned at all — TASK-219)
  - any magnitude, duration, threshold, condition, or lifetime
  - any comparison between two alternatives: there is no tooltip, no hover
    handler, and no detail panel in the Lobby (only a pointer-DOWN toggle), and
    the Collection viewer that does have a panel describes neither effects nor
    anything beyond the wire members
  - the gameplay consequence of a choice before START BATTLE, in any form
  - the shape of the choice at all: with the starter grant, the player owns
    exactly 3 Basic Cards and 3 Relics against an exactly-3 / 3–5 requirement
    (TASK-212 §A-05), so the "choice" is a permutation — but that is a
    reachability question (TASK-213), not a content-contract one
```

### 4.4 Presentation constraints discovered (recorded for the implementation task)

```text
1. `LobbyScene.ts` is test-pinned to contain none of the lowercase substrings
   `damage`, `cascade`, `combo`, `power`, `matchCount`, `isEquipped`,
   `loadoutPosition`, `validate` after comment stripping
   (LobbyScene.test.ts:1496-1515). Any new Lobby code must respect that rule —
   the check is case-sensitive, so a raw wire token ("Power", "Damage",
   "OnCombo", "OnCascade") is safe while an identifier such as `powerGrant` or
   `damageValue` is not. The rule must NOT be weakened; if a presentation table
   genuinely needs that vocabulary, it belongs in a separate presentation module
   (the documented boundary is about the scene holding no gameplay logic).
2. The bottom text band is geometry-pinned: `selectionText` 3 lines,
   `reviewText` 4 lines, `messageText` 1 line, with fixed offsets and gaps
   (LobbyScene.test.ts:1556-1599; LobbyScene.ts:122-141). Adding lines to those
   blocks requires its own layout assertion, not a weakened one.
3. Row width budget: column pitch 372 px, 14 px monospace, origin
   `SAFE_AREA.x + 26` (LobbyScene.ts:101-106, :1324-1330) — roughly 40–44
   characters per row before the next column. A Card row already uses ~22
   (`● Power Charge  [Basic]`); a Relic row ~22 with a slot suffix. A short
   effect suffix fits; a trigger + condition + effect for a Relic, or a
   two-effect Card, does not fit on one line.
4. The exact presentation (row suffix, a second line, a selected-item detail
   block, or a hover surface) is an implementation decision, NOT part of this
   contract decision, provided it obeys §6.5's rules. This audit does not
   prescribe typography or wording.
```

### 4.5 Conclusion

The loadout decision point currently communicates **identity without meaning**.
That is the gap `GDD.md:303-306` and pillar 2 describe, and it is a happy-path,
every-session gap: the player passes through this screen before every battle.

---

## 5. Authoritative Requirement

### 5.1 The requirement, quoted

```text
GDD.md:301-309  §17 Design Philosophy
  "The player should always understand: what Gems do, why a Match is valuable,
   what their Pet Passive does and when it triggers, how Elements affect damage,
   what each Card/Relic changes, why the Boss is dangerous, and what strategic
   choice comes next."
  → the operative clause is "what each Card/Relic changes"

GDD.md:57-67    §1.3 pillar 2 "Build Diversity — Pets, Cards and Relics allow
                different approaches to the same battle"

GDD.md:71-83    §2 the core loop, whose loadout leg is
                "Equip Cards → Equip Relics → Choose Boss → Start Battle"
                (reached again through PLAY AGAIN with the loadout preserved)

GDD.md:200-212  §8 Card System — "Cards are active player choices"; the exact
                Power costs and effects are delegated to CARD_RULES.md
GDD.md:229-237  §10 Relic System — "Relics passively modify the build … can
                trigger from events such as Matches, Combo, Damage, HP
                thresholds, or Card casts"; the exact list/triggers are delegated
                to RELIC_RULES.md

MVP_SCOPE.md:59-70  §1 IN — "3 Basic Cards … 5 Pet Skill Cards" and "~10 Relics,
                    Trigger system …, 3–5 equipped Relics per battle"
MVP_SCOPE.md:143-150 §4 Classification Authority — absent from §1 and §2 means
                    FUTURE; report ambiguity rather than assume IN
```

### 5.2 Interpretation — what is required, what is merely desirable, and what is out of scope

```text
1. REQUIRED (this decision). The player must be able to learn, at the loadout
   decision point and for each Card and Relic offered there, WHAT IT CHANGES:
   the effect identity, and the magnitude/parameters the owning rule document
   authors. GDD §17 names Card and Relic content explicitly; GDD §8/§10 delegate
   the *what* to CARD_RULES.md and RELIC_RULES.md, and those documents express it
   as structured data (§ below). This is the whole requirement.

2. REQUIRED, and it is the reason the description cannot be composed
   client-side. The effect identity must come from structured content, never
   from prose, from a Name, or from an id mapping — RELIC_RULES.md §8.2 item 1
   states the prohibition for Relics in exactly those words, DATABASE.md
   §1 item 1 states the same for Cards, and TASK-082 R2-7's verbatim-prose
   contract was deliberately superseded for both members
   (DATABASE.md:648-663,716-752). No authored prose remains that a server or a
   client could quote.

3. DESIRABLE, NOT REQUIRED HERE.
   - A human sentence rather than structured tokens. Nothing in docs/ requires
     natural-language phrasing; GDD §17 requires understanding, and the domain
     documents' own §6 tables are token-plus-magnitude tables.
   - Rarity, lore, upgrade level, or art: no document defines any of them
     (CollectionViewerScene.ts:716-727 records the same absence).
   - The Signature Skill's content at the loadout point: MVP_SCOPE.md §1 lists
     it IN, but the Signature Skill is DERIVED, not selected
     (API_CONTRACTS.md:380-405), so it is not a loadout decision — it is
     TASK-219 (§8).
   - Showing the *effective* Card cost or affordability: required by
     GDD.md:216-222 and GDD.md:9's spend/save tension, but it is a realtime
     projection question owned by TASK-212B and explicitly out of scope here
     (§1.6, §6.4).

4. NOT REQUIRED, EXPLICITLY.
   - Rarity: undefined.
   - Acquisition/unlock state: already expressed by array membership
     (API_CONTRACTS.md:783-786); no member is needed.
   - "Stats": Cards and Relics have no stats — their only content is
     `PowerCost` (cost, §6.4) and the effect array (this decision); Relics have
     `Trigger`, `Condition`, `EffectDefinition` and no stats at all
     (DATABASE.md:430-461).
   - Cost: excluded by this task's boundary, and it has a different owner.
```

### 5.3 What the domain documents already enumerate (the field set follows from this)

```text
Cards    CARD_RULES.md:124-151 §2 — each Basic Card is "Cost: N Power / Effect: …"
         CARD_RULES.md:355-453 §4.1 — each Pet Skill Card is "Cost / Effect / Burn"
         DATABASE.md:528-613 — the stored structured member set:
         effectType ∈ {Heal, Shield, Power, Damage, Burn, Crit}
         valueType  ∈ {Flat, PercentMaxHp, PercentagePoints, Undetermined}
         value      int > 0, present iff valueType interprets one
         duration   int > 0, present iff effectType = Burn
         scope      "NextAttack", present iff effectType = Crit

Relics   RELIC_RULES.md:472-495 §3 — the closed Trigger list (12 values)
         RELIC_RULES.md:811-874 §8.1 — Condition forms (3) + integer threshold
         RELIC_RULES.md:876-925 §8.2 — EffectDefinition[] members
         RELIC_RULES.md:927-987 §8.3 — the target/lifetime/valueType table
         RELIC_RULES.md:1011-1026 §8.5 — the canonical row set (transcribed)
         DATABASE.md:430-461 — the stored columns: Trigger, Condition (jsonb,
         NULL), EffectDefinition (jsonb, NOT NULL)
```

**The requirement therefore needs exactly the definition's own structured
content — nothing added, nothing paraphrased.** The vocabulary already exists and
is already closed; presenting it requires no new rule, and the §5.1 wire-value
precedent (the Element wire set, whose Vietnamese display names are "display
values only", `API_CONTRACTS.md:715-726`) shows the project already separates a
closed wire token from its presentation.

---

## 6. Contract Decision

```text
AMEND BOTH
```

`API_CONTRACTS.md` §5.3 (Cards) **and** §5.4 (Relics) are amended by
**additively exposing the definition's own already-authored structured content**,
with no new vocabulary, no authored prose, no cost member, no realtime member,
and no membership-semantics change.

### 6.1 Options considered

```text
NO CHANGE                       REJECTED — §1.3/§1.4: the client cannot represent
                                an effect at all, and no substitute is
                                authorized. GDD.md:303-306 stays unmet on the
                                happy path before every battle.

AMEND CARD READ CONTRACT ONLY    REJECTED — the Lobby asks for three Cards AND
                                three Relics; the Relic half is the larger
                                comprehension gap (a Relic's effect is never
                                discoverable in battle either, §8).

AMEND RELIC READ CONTRACT ONLY   REJECTED — same reason, symmetrically; the
                                Basic Cards are the *only* cards a player can
                                currently choose.

AMEND BOTH                       ACCEPTED — one rationale, one decision kind,
                                two response member lists, one implementation.

SPLIT INTO MULTIPLE DECISIONS    REJECTED as the decision — the two amendments
                                share one source pattern (expose the definition's
                                own structured member) and one rule set. If the
                                reviewer enforces one contract owner per task,
                                the natural split is TASK-212A-1 (Cards, §5.3) /
                                TASK-212A-2 (Relics, §5.4) with the Card half
                                first; §7 records that fallback, it is not the
                                decision.

Server-composed display text (a `description` string)   REJECTED — no document
                                authors Card/Relic prose: TASK-082 R2-7's
                                verbatim-text contract was superseded for both
                                members (DATABASE.md:648-663,716-752), and the
                                only prose that ever existed (the original
                                provisioning migration text, e.g. "Deal high
                                Fire (Hỏa) damage; apply Burn" —
                                PetCardRelicDefinitionProvisioningTests.cs:
                                312-343) is historical, inconsistent with the
                                authored magnitudes, and no longer stored. A
                                composed string would be presentation content
                                authored in a document that owns no such rule,
                                would need its own localization and its own
                                mapping table, and would be a second
                                representation of the same facts.

Client-side content catalog      REJECTED — RELIC_RULES.md §8.2 item 1 forbids
                                deriving a Relic's effect from its Name or from
                                a RelicDefinitionId mapping, and hardcoded
                                per-Relic logic is forbidden by name; a client
                                catalog is a second source of truth
                                (AGENTS.md §7/§9). No test currently forbids one
                                (§9.4), which is exactly why the decision must.
```

### 6.2 Proposed Cards amendment — `API_CONTRACTS.md` §5.3

```json
[ { "cardId": "card-heal", "name": "Heal", "category": "Basic",
    "effectDefinition": [
      { "effectType": "Heal", "valueType": "PercentMaxHp", "value": 20 } ] } ]
```

| Field | Source of truth | Wire type | Nullability / presence | Example | Player-facing purpose | Can existing client data provide it? |
|---|---|---|---|---|---|---|
| `effectDefinition` | `CardDefinition.EffectDefinition` — storage shape owned by `DATABASE.md` §1; member names fixed by §1 items 1-3; values owned by `CARD_RULES.md` §2/§4.1 | array of objects, length ≥ 1 (the column is NOT NULL; a one-effect Card is a one-element array, TASK-111 D-1b) | **Always present. Never null. Never empty.** | `[{"effectType":"Heal","valueType":"PercentMaxHp","value":20}]` | What the Card changes — the effect identity plus the magnitude and its interpretation | **No.** `CollectionModels.ts:20-26,110-124` has no such member and forbids adding it; nothing else in `src/` carries Card content |
| `effectDefinition[].effectType` | `DATABASE.md` §1 item 1 (closed set); identities owned per `CARD_RULES.md` §2/§4.1 | string — `"Heal"` \| `"Shield"` \| `"Power"` \| `"Damage"` \| `"Burn"` \| `"Crit"` | always present | `"Heal"` | Which effect the Card applies | No |
| `effectDefinition[].valueType` | `DATABASE.md` §1 item 1 / §3 | string — `"Flat"` \| `"PercentMaxHp"` \| `"PercentagePoints"` \| `"Undetermined"` | always present | `"PercentMaxHp"` | How the magnitude is interpreted (a plain amount, a share of Max HP, or percentage points) | No |
| `effectDefinition[].value` | `CARD_RULES.md` §2/§4.1 (magnitudes), `DATABASE.md` §3 (presence rule) | int > 0 | **present iff `valueType` interprets one**; absent for `Undetermined` (never `0`, never `null` — §3, and no provisioned row is in that state today) | `20` | The effect's magnitude | No |
| `effectDefinition[].duration` | `CARD_RULES.md` §4.1 (Burn); `DATABASE.md` §3 (presence rule) | int > 0, in Turns | **present iff `effectType = "Burn"`**; absent otherwise | `2` | How many Turns a Burn lasts | No |
| `effectDefinition[].scope` | `CARD_RULES.md` §4.1; `DATABASE.md` §3 (presence rule) | string — the single defined value `"NextAttack"` | **present iff `effectType = "Crit"`**; absent otherwise | `"NextAttack"` | Which damage instances a Crit increase applies to | No |

Notes the amended section must state (each already a rule elsewhere; §5.3
references, never restates):

```text
1. The member names are DATABASE.md §1's stored member names; §5.3 adds no
   vocabulary of its own and restates neither the closed sets nor the
   present-iff conditions — it references DATABASE.md §1/§3 and CARD_RULES.md
   §2/§4.1, exactly as the existing `category` row references CARD_RULES.md §1
   and DATABASE.md §3.
2. `playerId`, `powerCost` and `loadoutCopyLimit` remain NOT exposed. The
   not-exposed sentence loses only `effectDefinition`.
3. Membership semantics are unchanged: presence in the array is still the
   unlocked state, there is still no `unlocked` member, and a `PetSkill`
   CardDefinition is still not an unlock row (CARD_RULES.md §1 item 4, ADR-012
   item 9). The member is definition-generic; no PetSkill row reaches it because
   none is ever an unlock row.
4. No equip/loadout member is added (§5.6 unchanged).
5. Nothing in this amendment authorizes a cost, an affordability state, a
   legality judgment, or any `CardCostModifiers[]` reconstruction
   (SIGNALR_PROTOCOL.md §4 item 15 remains in force).
```

**Explicitly NOT added to §5.3:** `powerCost` (TASK-212B — §6.4),
`loadoutCopyLimit` (§2.4), `definitionId` (no such concept for Cards),
`playerId`.

### 6.3 Proposed Relics amendment — `API_CONTRACTS.md` §5.4

```json
[ { "relicId": "relic-instance-1", "name": "Berserker Core",
    "trigger": "OnMatchCount",
    "condition": { "conditionType": "MatchCountAtLeast", "threshold": 3 },
    "effectDefinition": [
      { "effectType": "ATK", "valueType": "Percentage", "value": 5,
        "target": "Pet", "lifetime": "Battle" } ] } ]
```

| Field | Source of truth | Wire type | Nullability / presence | Example | Player-facing purpose | Can existing client data provide it? |
|---|---|---|---|---|---|---|
| `trigger` | `RelicDefinition.Trigger`; the closed value set is `RELIC_RULES.md` §3 (12 values, referenced not restated) | string | **Always present. Never null.** (`DATABASE.md` §1 stores it NOT NULL; §8.5 item 3 keeps one primary Trigger) | `"OnMatchCount"` | When the Relic reacts | **No.** `RelicResponse` has two members and no content source exists in `src/` |
| `condition` | `RelicDefinition.Condition` — structured per `RELIC_RULES.md` §8.1; stored per `DATABASE.md` §1 (jsonb, NULL) | object `{ conditionType: string, threshold: int }`, or `null` | **Nullable — the one genuinely optional member.** `null` iff the Relic declares no extra condition (`RELIC_RULES.md` §8.1 item 4). Emitted as explicit `null` so the member set is fixed and exactly assertable, since `null` is already the storage's spelling of "no condition" (`DATABASE.md` §1) and absence would otherwise be ambiguous between "none" and "unsupported" | `{"conditionType":"MatchCountAtLeast","threshold":3}` | The threshold the reaction waits for | No |
| `condition.conditionType` | `RELIC_RULES.md` §8.1 (closed set of 3) | string — `"MatchCountAtLeast"` \| `"ComboAtLeast"` \| `"HpPercentageBelow"` | always present when `condition` is not `null` | `"MatchCountAtLeast"` | Which comparison form applies | No |
| `condition.threshold` | `RELIC_RULES.md` §6/§8.5 (the `N` values) | int > 0 | always present when `condition` is not `null` | `3` | The comparison's `N` | No |
| `effectDefinition` | `RelicDefinition.EffectDefinition` — structure owned by `RELIC_RULES.md` §8.2-§8.3 | array of objects, length ≥ 1 (NOT NULL; at least one effect per `DATABASE.md` §1) | **Always present. Never null. Never empty.** | `[{"effectType":"ATK","valueType":"Percentage","value":5,"target":"Pet","lifetime":"Battle"}]` | What the Relic changes | No |
| `effectDefinition[].effectType` | `RELIC_RULES.md` §8.2 item 1 (closed set) | string — `"ATK"` \| `"Power"` \| `"Crit"` \| `"CardCost"` \| `"BurnDamage"` | always present | `"ATK"` | Which effect the Relic applies | No |
| `effectDefinition[].valueType` | `RELIC_RULES.md` §8.2 item 2 / §8.3 table | string — `"Flat"` \| `"Percentage"` \| `"PercentagePoints"` \| `"Undetermined"` | always present | `"Percentage"` | How the magnitude is interpreted | No |
| `effectDefinition[].value` | `RELIC_RULES.md` §6/§8.5 (magnitudes) | int > 0 | **present iff `valueType` interprets one**; absent for `Undetermined` (never `0`, never `null`; no provisioned row is in that state) | `5` | The effect's magnitude | No |
| `effectDefinition[].target` | `RELIC_RULES.md` §8.3 item 1 — the single defined value `Pet` | string — `"Pet"` | always present | `"Pet"` | Kept because it is a required member of the effect contract; it is constant today and carries no distinguishing information | No |
| `effectDefinition[].lifetime` | `RELIC_RULES.md` §8.3 item 2 / §8.4 item 5 (closed set of 3) | string — `"Immediate"` \| `"Battle"` \| `"NextAttack"` | always present | `"Battle"` | How long the change persists | No |

Notes the amended section must state:

```text
1. The member names and the token vocabularies are the definition's own
   (DATABASE.md §1 storage names; RELIC_RULES.md §3/§8.1-§8.3 owners). §5.4 adds
   no vocabulary and restates no closed set — it references them, as it already
   references RELIC_RULES.md §2.2 for `relicId`.
2. `playerId`, `acquiredAt` and `definitionId` remain NOT exposed; the
   not-exposed sentence loses only `Trigger`/`Condition`/`EffectDefinition`.
3. The content is delivered per owned INSTANCE, exactly as `name` already is;
   two instances of one definition repeat the content and are still two
   elements. No definition→content catalog is created client-side.
4. No `Reset`/`Cooldown` member is added: DATABASE.md §1 stores none and
   RELIC_RULES.md §8.4 item 4 adds no per-Relic cooldown, charge, or reset state.
5. No equip/loadout member is added (§5.6 unchanged); `relicId` remains the
   instance identity submitted in `relicLoadout` (§3), unchanged in meaning.
6. This amendment does NOT deliver anything on the SignalR wire:
   RelicTriggered keeps its `{ type, relicId }` shape
   (SIGNALR_PROTOCOL.md §3.2.23; RelicWireProjectionTests.cs:46-74).
```

**Explicitly NOT added to §5.4:** `definitionId` (§3.4), `acquiredAt`,
`playerId`, any `Reset`/`Cooldown` member.

### 6.4 Boundary — content, never cost

```text
IN  (TASK-212A)   what a Card/Relic does: effect identity, magnitude,
                  interpretation, duration, scope, trigger, condition,
                  lifetime, target
OUT (TASK-212B)   what a cast costs right now: powerCost, current cost,
                  modified cost, affordability, legality, CardCostModifiers[],
                  Power requirements
```

`SIGNALR_PROTOCOL.md` §4 item 15 explicitly leaves the realtime member undelivered
and forbids the client to "compute, predict, or reconstruct a Card-cost modifier
from events, from a `PowerChanged` delta, or from a Card's definition"; the
composed value is `EffectiveCardCost` (`CARD_RULES.md` §3.6). Adding `powerCost`
to §5.3 would therefore be *misleading* whenever a modifier is active and would
still leave affordability forbidden — which is precisely why §5.3's widening here
excludes it. One cross-reference is recorded for whichever task owns cost:
`SIGNALR_PROTOCOL.md:1099` ("A client that must show the spent Cost reads the
Card's definition") must be reconciled with §4 item 15 by TASK-212B, not by this
decision. Nothing in this amendment authorizes an in-battle or Lobby cost member.

### 6.5 Client presentation rules recorded with the decision

```text
1. Presentation may translate the tokens and the structured parameters into
   display text. It may NOT state a rule the content does not carry — no
   magnitude, threshold, duration, or lifetime that is absent from the response.
2. It may NOT compute: no resolving `PercentMaxHp` against a Pet's Max HP, no
   damage prediction from a `Damage` element, no effective cost, no
   affordability, no legality, no interpolation of an absent optional member
   (AGENTS.md §10; SIGNALR_PROTOCOL.md §4 item 15).
3. It MUST fail closed: an unrecognized token, a malformed element, or an absent
   member renders the raw token or omits the line — never a guessed name or a
   fabricated value. This is the TASK-208 §D "never a guessed name" rule
   (SceneLifecycle.test.ts:2479-2513; BattleEventPresentation.test.ts:596-655),
   extended to the new text.
4. It must NOT become a second content catalog: no per-CardId or
   per-RelicDefinitionId lookup table, no hardcoded magnitudes
   (RELIC_RULES.md §8.2 item 1; AGENTS.md §7/§9).
5. `Damage` is a Card/Skill base value entering the Damage Pipeline
   (COMBAT_RULES.md §3 step 1), not a dealt amount; a `PercentMaxHp` is a
   proportion; a `CardCost` effect is a cost modifier, not a cost. The
   presentation must not imply a computed result (AGENTS.md §10).
```

### 6.6 Signature Skill separation (verified, not resolved)

```text
Can the normal Card/Relic content contract remain valid WITHOUT solving the
Signature Skill?  YES.
  - GET /api/cards membership = unlock rows; a PetSkill CardDefinition is never
    an unlock row (CARD_RULES.md §1 item 4; ADR-012 item 9), so the Signature
    Skill is not returned by the endpoint this decision widens
    (API_CONTRACTS.md:783-786).
  - The Lobby's card decision is exactly 3 Basic Cards (CARD_RULES.md §1;
    API_CONTRACTS.md:380-405), so the widened member delivers the whole loadout
    decision for Cards today.
  - The widened member is definition-generic (its vocabulary already covers
    Damage/Burn/Crit), so TASK-219 may reuse the same member shape if it decides
    to expose the derived Signature Skill's definition through §5.1 or §3 — but
    this decision neither performs nor prejudges that, and it must NOT be read as
    making a PetSkill card an unlock row.
  - The in-battle fourth tile remains mislabelled/mis-dispatched (TASK-212 §A-01,
    /A-02) and stays TASK-219.
Dependency recorded, not resolved: TASK-219.
```

### 6.7 Contract ownership

```text
API_CONTRACTS.md §5.3   → owns the Card response member list (THIS decision)
API_CONTRACTS.md §5.4   → owns the Relic response member list (THIS decision)
DATABASE.md §1/§3       → owns the stored member names and present-iff rules
                          (referenced, never restated)
CARD_RULES.md §2/§4.1   → owns Card effect identities and magnitudes
                          (unchanged)
RELIC_RULES.md §3/§6/§8 → owns the Trigger set, Condition forms, effect
                          vocabulary, and magnitudes (unchanged)
SIGNALR_PROTOCOL.md §4  → owns the realtime projection (UNTOUCHED)
GDD.md / MVP_SCOPE.md   → own the product requirement (UNCHANGED — MVP_SCOPE §1
                          already lists the content as IN)
Migration / schema      → NOTHING (no column, index, or migration is involved)
ADR                     → NONE REQUIRED (no architecture, storage, realtime,
                          authoritative-model, or module-boundary change; the
                          precedent is TASK-070 D1-D4, which defined §5.3/§5.4
                          as documentation)
```

---

## 7. Recommended Implementation Task

```text
Task ID                 TASK-212A-1
Title                   Expose Card and Relic content on the §5.3/§5.4 read
                        contracts and present it at the loadout decision point
Priority                P1
Type                    Contract amendment + backend projection + client
                        presentation (one task; the TASK-208 → TASK-208A → TASK-209
                        shape)
```

**Objective.** Make the player able to understand what each Card and each Relic
offered at the loadout decision point changes — using only the definition's own
structured content, without widening the realtime projection, without inventing a
second content catalog, and without computing cost, affordability, or any
gameplay value.

**Scope.**

```text
1. Amend API_CONTRACTS.md §5.3 and §5.4 exactly as §6.2/§6.3 decide, with the
   version preamble recording the change, and re-read every site that restates
   the two member lists (§5.5, §5.6, §6, §1's endpoint summary, and any code
   comment that restates them — CollectionResponses.cs, CollectionController.cs,
   CardCollectionItem.cs, RelicCollectionItem.cs, CollectionModels.ts).
2. Widen the Application read models and the Api response records for exactly the
   new members (§6.2/§6.3) and nothing else.
3. Present the new members:
   - Lobby Card and Relic rows (the loadout decision point).
   - The Collection viewer's Card and Relic detail panels (so the two surfaces
     cannot contradict each other).
   The exact surface/layout is the implementer's decision inside §4.4's pinned
   constraints and §6.5's rules.
4. Fail closed on an absent/unknown/malformed member — raw token or omitted line,
   never a fabricated value (§6.5 item 3).
```

**Non-goals.**

```text
* No cost member, no affordability, no legality, no CardCostModifiers[], no
  Power requirement, no in-battle cast-tile change (TASK-212B).
* No SignalR / realtime / GAME_STATE / GAME_EVENTS / REDIS_STATE change; no new
  event, method, payload member, or projection.
* No ADR.
* No migration, no schema change, no new column, index, join, or query (the
  values are already read).
* No change to §5.3/§5.4 membership semantics; no `unlocked` member; a PetSkill
  CardDefinition stays a non-unlock row.
* No `definitionId`, `acquiredAt`, `playerId`, `powerCost`, or `loadoutCopyLimit`
  exposure.
* No Signature Skill read source, label, or dispatch change (TASK-219).
* No Relic trigger callout in battle (TASK-218); RelicTriggered keeps its shape.
* No acquisition/ownership/unlock change (TASK-213); no tier/star/passive scope
  change (TASK-215); no documentation sweep or architecture reconciliation
  (TASK-217); no auth/session work.
* No Card/Relic rule change; no new Prompt/Presentation requirement beyond
  displaying what the response carries.
* No client-side content catalog, no hardcoded magnitude, no per-id mapping
  (§6.5 item 4).
```

**Expected files/areas.**

```text
docs/02-technical/API_CONTRACTS.md        §5.3 / §5.4 (the decided widening),
                                          version preamble, §5.5/§5.6 re-read
src/backend/GameServer.Application/
    Collection/CardCollectionItem.cs      + the new Card member
    Collection/RelicCollectionItem.cs     + the three new Relic members
    Collection/CollectionQueryService.cs  carry the already-loaded definition
                                          values through the projection
src/backend/GameServer.Api/Controllers/
    CollectionResponses.cs                the two response shapes + their docs
    CollectionController.cs               doc comments only
src/frontend/client/src/services/api/CollectionModels.ts   the two client models
src/frontend/client/src/game/scenes/LobbyScene.ts          Card + Relic rows
src/frontend/client/src/game/scenes/CollectionViewerScene.ts  detail panels
(a separate presentation module if §4.4 item 1 requires one — the scene must
 stay free of gameplay vocabulary)
NOT touched: SIGNALR_PROTOCOL.md, GAME_STATE.md, GAME_EVENTS.md, REDIS_STATE.md,
             DATABASE.md, migrations, BattleHub.cs, BattleScene.ts,
             GameRuntime*.ts, any game-rule document
```

**API changes.** `API_CONTRACTS.md` §5.3: add `effectDefinition`.
`API_CONTRACTS.md` §5.4: add `trigger`, `condition`, `effectDefinition`. Both
**additive** on existing `200` responses; no route, status, error code, envelope,
pagination, ordering, or authentication change; `401`/empty-array behaviour
unchanged. No request changes.

**Backend changes.** Application read models + projection + Api response records
(one member each for Cards, three for Relics). **No repository, query, EF
configuration, migration, or stored-data change**: the values are already loaded
(`CardRepository.cs:47-64`, `RelicRepository.cs:89-104`). The projection is a
pure field mapping and computes nothing (`CollectionQueryService.cs:36-40`).

**Frontend changes.** `CollectionModels.ts` gains the members; the Lobby renders
the Card/Relic content; the Collection viewer renders it in both detail panels.
No change to selection, submission, preserved-loadout, error, or run-guard
behaviour.

**Tests.**

```text
Update (these currently PIN the exclusion, so they must be amended WITH the
change, not weakened):
  tests/backend/GameServer.Api.Tests/CollectionEndpointTests.cs
      :552-579  exact Card member set  → + effectDefinition
      :662-703  forbidden-name list    → remove effectDefinition; keep
                                         playerId/powerCost/loadoutCopyLimit
      :730-755  exact Relic member set → + trigger/condition/effectDefinition
      :811-851  forbidden-name list    → remove trigger/condition/effectDefinition;
                                         KEEP the "no rule text at any depth"
                                         intent: re-point it at the still-withheld
                                         values (playerId, acquiredAt,
                                         definitionId) and keep the equip-member
                                         assertions
  tests/backend/GameServer.Application.Tests/CollectionQueryServiceTests.cs
      :378-465 (Cards), :510-603 (Relics) — same two moves on the read models
  src/frontend/client/tests/CollectionService.test.ts
      :330-350 (Cards), :398-432 (Relics) — same two moves on the client model
Add:
  Cards   a Card with several effects and a Card with one effect both project
          faithfully; a Burn element carries duration and no scope; a Crit element
          carries scope and no duration; no member is invented and no value is
          recomputed
  Relics  a Relic with no Condition emits null; the four provisioned Conditions
          project their form and threshold; an element carries exactly
          effectType/valueType/value/target/lifetime
  Both    the still-withheld values remain absent (playerId, powerCost,
          loadoutCopyLimit / definitionId, acquiredAt) and no §5.6 equip member
          appears
  Client  Lobby rows render the content; the Collection panels render the content;
          an unknown token or absent member degrades to the raw token/omission
          with no fabricated value and no unhandled rejection; the loadout rules,
          submission and preserved-loadout behaviour are unchanged
  Client  no cost/affordability/legality computation is introduced — extend
          RuntimeBoundaries.test.ts's forbidden-term set (:460-482) rather than
          weakening it, and keep LobbyScene.test.ts:1496-1515 intact (move any
          needed vocabulary into a separate presentation module instead of
          relaxing the rule)
  Client  no client-side per-id content table is introduced
```

**Verification.**

```text
Required commands
  git diff --check                                   PASS
  npx tsc --noEmit                                   PASS
  npx vitest run                                     PASS (817 existing tests
                                                     unchanged or extended)
  dotnet test (Api + Application + Domain +
              Infrastructure)                        PASS
  npm run verify:e2e:smoke                           PASS (132 checks × 2 runs)
Required new coverage
  a Lobby check that Card and Relic content text is present on the player's
  screen before START BATTLE
Required documentation
  API_CONTRACTS.md §5.3/§5.4 amended and the version preamble updated; every
  site that restates the two member lists re-read for consistency
```

**Estimated effort.** **S–M**: the contract amendment and the backend projection
are mechanical (existing data, existing records, one/serial three members); the
client work is the larger half (two screens plus the presentation and fallback
rules). If split by type: contract + backend **S**, client **S–M**.

**Reserved-ID note.** `TASK-212A-1` is the sanctioned child ID from TASK-212's
own §5 ("if the reviewer enforces one type per task, split the decision record
and the client rendering into TASK-212A-1 / TASK-212A-2"). If that split is
enforced: **TASK-212A-1** = the contract amendment + backend projection
(`API_CONTRACTS.md` + Application/Api), **TASK-212A-2** = the client rendering.
No existing ID is reused (this audit is `TASK-212A`; `TASK-212B` and
`TASK-213…219` are reserved by prior records).

---

## 8. Dependencies / Follow-ups

```text
TASK-212B — In-battle Card cost and affordability (SIGNALR_PROTOCOL.md §4)
  Separate, NOT merged here. Owner: the realtime projection. Blocked by its own
  protocol decision AND by TASK-213 (the only provisioned CardCost source,
  Emergency Core, is unreachable — TASK-212 §A-04/§A-05). Two cross-references
  this audit hands forward:
    (1) §4 item 15 forbids reconstructing a cost modifier "from a Card's
        definition" while SIGNALR_PROTOCOL.md:1099 says a client that must show
        the spent Cost reads the Card's definition — reconcile these in 212B.
    (2) This amendment deliberately does not expose `powerCost`; if 212B decides
        to expose it, that is a §5.3 change owned by 212B's decision, not a
        defect in this one.

TASK-213 — Content reachability (5 Pets / ~10 Relics / 5 Pet Skill Cards vs
  starter-only ownership). Independent of this decision, but it gates the
  *value*: with 3 owned Cards and 3 owned Relics against exactly-3 / 3–5
  requirements, the loadout is a permutation, so comprehension improves without
  changing a build. This amendment must not be read as resolving it, and §5.3's
  membership semantics must not be stretched to compensate for it.

TASK-219 — Pet Signature Skill read source (and the wrong action its absence
  causes: BattleScene sends CardCast instead of PetSkillCast). Separate decision,
  NOT absorbed (§6.6). Verified NOT blocking this amendment. If TASK-219 exposes
  the derived Signature Skill's definition, it may reuse the §5.3
  `effectDefinition` shape, but it owns that choice, and the Card/Relic content
  contract remains valid without it.

TASK-217 — Documentation and cross-reference sweep. This decision adds two
  member-list restatements that TASK-217B must keep consistent, and it does NOT
  resolve any drift it touched: MVP_SCOPE.md:68's `OnMatch` trigger claim
  (RELIC_RULES.md §3 lists it; RelicFiringPoint.cs:46-81 implements no firing
  point for it), and the stale "not implemented" comments in
  CardDefinition.cs:53-58 / RelicDefinition.cs:50-55,146-150. Reported, not fixed
  (AGENTS.md §16).

TASK-218 — Relic trigger callout in battle. Independent and implementation-only
  (TASK-212 §A-07); the name map it needs already exists. Sharing no file with
  this change except perhaps a presentation helper — coordinate if so.

TASK-215 — Documented-but-inert Pet progression/Passive axes. Unrelated.

TASK-212 §A-18 / §A-13 (presentation defects, no owner yet) — the Lobby's Step-5
  review block printing raw ids while the names are already loaded
  (LobbyScene.ts:1077-1084) is an implementation-only fix in the same file this
  change touches; it may ride along, but it is NOT part of this decision and must
  not be used to justify a contract member.

Newly discovered, reported (no owner yet):
  D1. The Lobby renders whatever GET /api/cards returns, unfiltered by category
      (LobbyScene.ts:1249-1261; the scene's own note at :531 says filtering would
      be a client-side legality decision). A PetSkill row would therefore be
      offered as a loadout option; today none is ever returned, so this is latent,
      not live. This amendment must NOT change membership; if the latent case is
      to be addressed it needs its own decision.
  D2. API_CONTRACTS.md:783-786's exclusion list and the five code/test sites that
      repeat it must move together in one change; this was true for every prior
      §5 member change and is recorded here so TASK-212A-1 does not miss
      CardCollectionItem.cs:13-21, RelicCollectionItem.cs:12-16,
      CollectionResponses.cs:88-93,123-129, CollectionController.cs:171-217,
      CollectionModels.ts:20-26.
  D3. ELEMENT_RULES.md §1/§6 + CARD_RULES.md:406-415 say a damage-dealing Pet
      Skill's Element is the Pet's, per effect, while the stored Card effect
      element carries no Element member and the executor reads
      state.PetState.Element (CardCastExecutor.cs:275). The two agree for all five
      MVP Pets, so this is NOT a live conflict — but it is the reason a
      Card-effect presentation cannot state a damage Element from the definition
      alone. Recorded so no presentation invents one.
  D4. tests/backend/GameServer.Api.Tests/TestProvisionedContent.cs:110-157 seeds
      Pet Skill values that differ from the Domain/provisioning values (Tidal
      Barrier 30/30, Iron Fang 150 / +30). The file documents itself as fixture
      content and is not a content source; recorded so a future implementer does
      not read it as authored data.
```

---

## 9. Evidence

### 9.1 Authoritative documents inspected

```text
GDD.md            §1.2, §1.3 (pillars), §2 (core loop), §7, §8, §9, §10, §14,
                  §15, §17
MVP_SCOPE.md      §1 (Cards, Relics, Pets, Player), §2, §3, §4
CARD_RULES.md     §1 (categories, ownership vs loadout, copy limit), §2 (Basic
                  Cards), §3 (casting), §3.6 (EffectiveCardCost), §4/§4.1 (Pet
                  Skill Cards), §5 (Cards vs Relics), §6 (events)
RELIC_RULES.md    §1 (structure), §2.1–§2.5, §3 (the closed Trigger list), §6
                  (MVP Relic reference), §7 (events), §8.1 (Condition), §8.2
                  (EffectDefinition), §8.3 (target/lifetime table), §8.4
                  (lifetime vs re-evaluation), §8.5 (the canonical contract)
ELEMENT_RULES.md  §1, §5, §6
DATABASE.md       §1 (CardDefinition, RelicDefinition, PlayerUnlockedCard,
                  Relic; the Card EffectDefinition contract :528-680; the Relic
                  note :716-752), §2, §3, §5
API_CONTRACTS.md  version preamble (:1-110), §3 (battle start, cardLoadout /
                  relicLoadout), §5.1, §5.2, §5.3, §5.4, §5.5, §5.6, §6
GAME_STATE.md     §0, §2 (EquippedCards[]/EquippedRelics[] and the projection
                  rules, :1298-1341)
SIGNALR_PROTOCOL.md §3.2.20 (CardCast, item 2), §3.2.21, §3.2.23, §4 items
                  14–16 (item 15 in full), §7
AGENTS.md / docs/AGENTS.md  §2, §4, §6, §7, §8, §9, §10, §14, §16, §17, §20, §22
tasks/README.md   §3 (ID convention), §5, §9
```

### 9.2 Source inspected (implementation)

```text
Api          CollectionController.cs, CollectionResponses.cs
Application  Collection/CollectionQueryService.cs, CardCollectionItem.cs,
             RelicCollectionItem.cs, ElementWireValues.cs;
             Cards/ICardRepository.cs, Relics/IRelicRepository.cs
Domain       Cards/CardDefinition.cs, CardEffectDefinition.cs,
             CardEffectType.cs, CardEffectValueType.cs, CardCastExecutor.cs;
             Relics/RelicDefinition.cs, RelicCondition.cs,
             RelicEffectDefinition.cs, RelicFiringPoint.cs
Infrastructure  Postgres/Repositories/CardRepository.cs, RelicRepository.cs
Client       game/scenes/LobbyScene.ts (rows, selection, review block, layout
             constants, renderOption), game/scenes/CollectionViewerScene.ts
             (detail panel, tabs, list truncation), services/api/CollectionModels.ts,
             services/api/ApiService.ts (getCards/getRelics), game/runtime/
             GameRuntime.ts, game/runtime/GameRuntimeEvents.ts (the port),
             game/scenes/BattleScene.ts (the battle-side Card definition use, for
             contrast only)
```

### 9.3 Tests inspected (not modified)

```text
Backend
  Api            CollectionEndpointTests.cs — read in full for the Card/Relic
                 sections (:540-945) plus the header contract note (:30-57);
                 RelicWireProjectionTests.cs (:46-94)
  Application    CollectionQueryServiceTests.cs (:330-609)
  Domain         CardEffectDefinitionTests.cs, CardEffectDefinitionsTests.cs
                 (:459-615), RelicStructuredContractTests.cs (:255-354),
                 RelicProvisionedDefinitions.cs, RelicResolverTests.cs,
                 Task177RelicFiringPointContractTests.cs, CardCastExecutorTests.cs
  Infra          PetCardRelicDefinitionProvisioningTests.cs (:300-359, :535-649),
                 RemainingMvpRelicDefinitionProvisioningTests.cs (:318-407),
                 RelicProvisionedContent.cs, TestProvisionedContent.cs
Client
  CollectionService.test.ts (:300-450), LobbyScene.test.ts (:60-99, :500-930,
  :1420-1600), CollectionViewerScene.test.ts (:40-90, :455-800),
  RuntimeBoundaries.test.ts (:234-355, :440-500, :656-722),
  GameRuntime.test.ts (:2489-2614), SceneLifecycle.test.ts (:308-313, :2470-2524),
  BattleEventPresentation.test.ts (:595-664), PreservedLoadout.test.ts
```

### 9.4 Verification commands actually run by this audit

```text
git status --porcelain / git status --short      same 26 modified tracked paths
                                                 and same 7 pre-existing untracked
                                                 records before and after this
                                                 audit; the only addition is this
                                                 record itself
git diff --check                                 PASS (exit 0)
direct check of the new record                   no trailing whitespace, no tabs,
                                                 LF endings
npx vitest run tests/CollectionService.test.ts tests/LobbyScene.test.ts
    tests/CollectionViewerScene.test.ts tests/RuntimeBoundaries.test.ts
    (src/frontend/client)                        PASS — 4 files, 219 tests
                                                 (CollectionService 27,
                                                 LobbyScene 83,
                                                 CollectionViewerScene 55,
                                                 RuntimeBoundaries 54)
```

### 9.5 Prior records inspected (not edited)

```text
TASK-212-post-task-211-product-audit.md   §1 items 3/4/6, §3 A-01…A-05, A-07,
                                          §4 (the SPLIT decision), §5 (the
                                          TASK-212A proposal), §7.3
TASK-207-product-roadmap-and-gameplay-gap-audit.md   TASK-212's originating
                                          entry (:428-437)
tasks/README.md, TASK_LIFECYCLE.md (read for record conventions only)
Live-E2E observations cited from TASK-212's record: phase4.starterGrantLoaded
  {pets:1, cards:3, relics:3}; phase5b.noDeveloperDiagnosticsInTheHud (the raw
  passive id on screen); phase6d.signatureSkillControlRemainsUsable
  ("CardCast card-inferno: rejected (INSUFFICIENT_POWER)")
```

### 9.6 Observations this audit could not fully resolve

```text
UNVERIFIED (not re-derived)  the 132-check live E2E result and the 2 919
  backend test count — both are TASK-212's recorded results at this tree; this
  audit ran no live stack and no backend suite.
OBSERVED (fixture, not a content source)  TestProvisionedContent.cs:110-157
  seeds Pet Skill values that differ from the provisioned ones — see D4.
OBSERVED (latent, not live)  the Lobby's category-agnostic Card list — see D1.
OBSERVED (documentation drift, reported not fixed)  MVP_SCOPE.md:68's `OnMatch`
  trigger vs RelicFiringPoint.cs:46-81; the stale "not implemented" comments in
  CardDefinition.cs:53-58 and RelicDefinition.cs:50-55,146-150; the Lobby's raw
  ids in its review block (LobbyScene.ts:1077-1084).
```

---

## 10. Explicitly Out of Scope for This Audit

```text
* No production code, test, migration, contract, ADR, or docs/ change.
* No implementation of the decided amendment (§7 is a proposal).
* No cost, affordability, legality, or CardCostModifiers[] work (TASK-212B).
* No SignalR, battle-state, event, Redis, or projection change.
* No Signature Skill resolution (TASK-219), no acquisition decision (TASK-213),
  no Tier/Star/Passive decision (TASK-215), no documentation sweep (TASK-217),
  no in-battle Relic callout (TASK-218).
* No Card/Relic rule change, no new content, no new magnitude, no new token.
* No fix of any pre-existing drift, defect, or task-record integrity issue
  discovered above; each is reported with its location (§8, §9.6).
* All pre-existing working-tree changes were left untouched.
```

---

## 11. Final Report

```text
TASK-212A AUDIT COMPLETE

Decision: AMEND BOTH
  5.3  GET /api/cards   + effectDefinition (the Card's own structured effect
                        array; DATABASE.md §1 member names; CARD_RULES.md §2/§4.1
                        values)
  5.4  GET /api/relics  + trigger, + condition (nullable), + effectDefinition
                        (the Relic's own structured content; RELIC_RULES.md
                        §3/§6/§8 owners)
  Excluded and deferred:  powerCost/current cost/affordability/legality/
                          CardCostModifiers (TASK-212B); loadoutCopyLimit;
                          definitionId; the Signature Skill read source
                          (TASK-219); every SignalR/state/event change

Recommended implementation task: TASK-212A-1
Implementation: NOT DONE
Production changes: NONE
Test changes: NONE
Audit record: tasks/completed/TASK-212A-content-contract-audit.md
```
