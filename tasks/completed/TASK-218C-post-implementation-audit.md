# TASK-218C — Post-Implementation Relic Callout Audit

**Type:** Audit (no implementation)
**Status:** COMPLETE
**Decision:** **MODIFY** (test-only; no production, contract, mechanic, or E2E defect)
**Parent task:** TASK-218B — "present the Relic trigger callout in battle" (implementation present,
uncommitted, no task record)
**Predecessor:** TASK-218A — Relic Trigger Presentation Audit (`Decision: PASS`)
**Audited change set:** 5 files, `997` insertions / `41` deletions, all uncommitted
**Scope of this record:** verification only. No production, test, script, or documentation change was
made, and no Git state was altered.

---

## 0. Verdict and one-paragraph summary

**MODIFY.** The implementation is **substantively correct on every criterion the task enumerates**:
the event source is the unchanged server-authoritative path, the Relic callout is exactly priority
`2`, the identity domain is the owned **instance** on both sides of the lookup, presentation is
delivered-name-only, an unresolved Relic fails closed, the batch still yields one callout, no
existing callout regressed, and no scope leaked.

The verdict is not PASS for **two test-quality findings inside the audited change set** — one of them
a genuine non-vacuity defect:

```text
D-1  DEFECT (low, test-only)
     src/frontend/client/tests/SceneLifecycle.test.ts:3701
     The test named "holds no Relic definition when the collection read fails, and never throws"
     never fails the read. SceneHarnessOptions (:97-116) declares no `relicsFailure`, the
     destructure (:119-126) drops it, and `getRelics` is `vi.fn(async () => relics)` (:341) —
     it always resolves. The test exercises an EMPTY SUCCESSFUL collection, not a rejected read,
     so it passes for a reason other than the one its name states. `relicsFailure` is a dead
     argument member. The behaviour itself is genuinely covered elsewhere (§8.3), so no required
     behaviour is unverified.

O-2  OBSERVATION (low, test-only)
     src/frontend/client/scripts/standalone-web-smoke.mjs:2517-2521
     `phase5c.comboCalloutUsesTheDeliveredValue` was loosened from "every real-resolution
     callout is MATCH or COMBO ×N≥2" to "every callout is an allow-listed FORM". The widening is
     NECESSARY (a real resolution may now legitimately emit `RELIC: <name>`) and is compensated
     by the new rejection record at :2527-2531 — but the record's name no longer describes what
     it checks, and the real-resolution path has no exact-equality check on a Relic line.
```

Neither finding is an implementation defect: no authoritative decision was violated, and no
required behaviour is left unverified. The smallest correction is **test-only** (§14).

---

## 1. Runtime event chain — unchanged, single path, server-authoritative

### 1.1 What the change set actually touches

```text
src/frontend/client/src/game/scenes/BattleEventPresenter.ts      52 +/ 8 -
src/frontend/client/src/game/scenes/BattleScene.ts               79 +/ 5 -
src/frontend/client/tests/BattleEventPresentation.test.ts       354 +/22 -
src/frontend/client/tests/SceneLifecycle.test.ts                179 +/ 0 -
src/frontend/client/scripts/standalone-web-smoke.mjs            333 +/ 6 -
```

`git status --porcelain` reports **exactly these five** modified paths and no others; no `.cs`,
`.md`, migration, configuration, or contract file is in the change set.

### 1.2 The chain the implementation sits on

```text
RelicResolver.Resolve                                         (Domain; UNCHANGED by this task)
  sourceIdentity = equipped.InstanceIdentity.Value            RelicResolver.cs:314
  triggered.Add(new RelicTriggeredEvent(sourceIdentity))      RelicResolver.cs:381
BattleEventWireProjection.RelicTriggered(relicId)             BattleEventWireProjection.cs:553-556
BattleHub → Clients.Group(battleId).SendAsync("ReceiveEvents", …)
GameRuntime.forwardBattleEvents → GameRuntimePort.onBattleEvents
BattleScene.handleBattleEvents → presentEventBatch            BattleScene.ts:1867-1889
  parseInBattleEvent                                          :1871   (untouched)
  presentedEventsLog.push(formatInBattleEvent(parsed))         :1877   (untouched; never rendered)
  presentCombatFeedback(presented)                             :1880   (untouched)
  selectBatchCallout(presented, resolveCardName, resolveRelicName)  :1882-1886  ◄── the only change
  showCallout(callout.message, callout.color)                  :1887-1889 (untouched, :2104-2125)
```

**Verified:**

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| `RelicTriggered` stays fully server-authoritative | **HOLDS** | The client added a **lookup only**. `describeEventCallout`'s new arm reads `event.relicId` and calls the caller's resolver (`BattleEventPresenter.ts:682-689`). No evaluation, threshold comparison, or trigger decision exists anywhere in the added code. |
| No second event or presentation path introduced | **HOLDS** | `presentEventBatch` gained no branch, listener, queue, or subscription; its only delta is the third argument at `:1885`. One callout Text (`:565`, `:2105`), one `showCallout` (`:2104`), one selection (`selectBatchCallout`, `BattleEventPresenter.ts:738`). No new module, class, scene, or notification system. |
| The parse/format path is untouched | **HOLDS** | `parseRelicTriggered` and `formatInBattleEvent` are absent from the diff. |
| No client-side Relic derivation | **HOLDS** | `loadRelicDefinitions` stores the delivered object and reads `name` only (`BattleScene.ts:1312`, `:2160`). No `.trigger`, `.condition`, `.effectDefinition` read exists in either scene (verified by grep and by the source-scan test at `SceneLifecycle.test.ts:3724`). |

---

## 2. Priority semantics — priority 2 preserved, tie rule intact

### 2.1 The ladder as implemented

```text
BattleEventPresenter.ts:593-599
  1  CALLOUT_PRIORITY_COMBO
  2  CALLOUT_PRIORITY_CAST
  2  CALLOUT_PRIORITY_RELIC        ◄── new (:595)
  3  CALLOUT_PRIORITY_BOSS
  4  CALLOUT_PRIORITY_PASSIVE_TRIGGERED
  5  CALLOUT_PRIORITY_PASSIVE_CHARGED
  6  CALLOUT_PRIORITY_MATCH
```

**No existing constant was renumbered.** The diff adds one constant and edits none; the tie rule
`candidate.priority <= selected.priority` (`:748`) is byte-identical, so **last-delivered-wins among
equals** is unchanged.

### 2.2 Every required relationship, and the test that pins it

| Relationship | Ladder fact | Test proving it |
| --- | --- | --- |
| Relic is exactly priority 2 | `:595` | `BattleEventPresentation.test.ts:670` — exact `toEqual({ message: 'RELIC: Berserker Core', color: '#7dd3fc', priority: 2 })` |
| Combo (1) outranks Relic (2) | 1 < 2 | `:907` `comboBatch` → `'COMBO ×3'`; scene-level `:1596` → `'COMBO ×3'` |
| Relic (2) ties Card/Pet cast (2) | 2 == 2 | `:920-924` `castBatch` (Relic then CardCast) → `'CARD: Heal'` — the **later** equal wins |
| Last-delivered tie behaviour intact | `:748` `<=` | `:880-881` two casts → `'PET SKILL: card-inferno'` (pre-existing assertion, unchanged) |
| Relic (2) outranks Boss (3) | 2 < 3 | `:909-915` `bossBatch` (Boss, Relic, Match) → `'RELIC: Berserker Core'` |
| Relic (2) outranks Match (6) | 2 < 6 | same `bossBatch`, which carries `MatchCreated` |
| Match (6) remains lowest | 6 == max | `:854` `toEqual({ …, priority: 6 })` (unchanged) |
| Boss (3) / Passive (4,5) unchanged | no edit | `:788` Boss priority 3; `:805` Passive 5; `:819`/`:834` PassiveCharged/Triggered |

**Two relations are not directly batched:** Relic vs `PassiveTriggered` (4) and Relic vs
`PassiveCharged` (5). They are nonetheless **provable, not assumed**: both endpoints are pinned by
exact value elsewhere (Relic = 2 at `:670`; PassiveCharged = 5 at `:805`; PassiveTriggered via the
unchanged `:834`), and the comparator is unchanged, so the ordering follows arithmetically from
constants that are themselves asserted. This is a coverage-completeness note, not a vacuity defect.

### 2.3 The documented rationale is accurate

The comment at `:576-591` justifies sharing a number rather than taking a new one, and states the
among-equals tie is delivery order "which is already how two casts in one batch are resolved". Both
halves are true of the code (`:748`), and the cast precedent is asserted at `:880-881`.

---

## 3. Relic identity mapping — the critical question

### 3.1 What `relicId` is, from authoritative source

**It is the owned Relic INSTANCE identity — not a definition identity.**

```text
EMISSION (the event)
  RelicResolver.cs:314   var sourceIdentity = equipped.InstanceIdentity.Value;   // = RelicInstanceId
  RelicResolver.cs:381   triggered.Add(new RelicTriggeredEvent(sourceIdentity));
  EquippedRelicIdentity.cs:4   "The identity of one owned Relic instance — the RelicInstanceId a …"
  PetState.cs:86               EquippedRelics[] carries the owned instance identity (RELIC_RULES.md §2.2)
  RelicWireProjectionTests.cs:54   Assert.Equal("relic_instance_7", wire.GetProperty("relicId")…)

DELIVERY (the read the scene performs)
  CollectionQueryService.cs:335    relic.RelicInstanceId,   // projected into the element
  CollectionResponses.cs:315       [property: JsonPropertyName("relicId")] string RelicId
  CollectionResponses.cs:301       <param name="RelicId"><c>Relic.RelicInstanceId</c> (§5.4).</param>
  CollectionController.cs:216-217  "relicId is the owned **instance** identity"
  CollectionEndpointTests.cs:1026-1028  "relicId = Relic.RelicInstanceId (§5.4) — the owned INSTANCE, never its definition"
```

Both sides are the **same identity domain**. The scene's map is keyed on the delivered member
itself:

```text
BattleScene.ts:1312   this.relicDefinitions.set(relic.relicId, relic);
BattleScene.ts:2160   return this.relicDefinitions.get(relicId)?.name ?? null;
```

so an **instance in → instance lookup → name out**. A definition-keyed map would never match a
delivered event, and the ladder would silently produce no callout — which the tests below would
catch.

### 3.2 Two owned instances of one definition, both equipped

**This case is real, and the mapping handles it correctly. It is proved from rules, not assumed.**

| Step | Authority | Evidence |
| --- | --- | --- |
| Loadout acceptance constrains **instance** identity, not definition identity | `RELIC_RULES.md` §2.4 item 3 | `RelicLoadoutService.cs:120-129` rejects only a repeated `RelicInstanceId` (`HasDuplicate`, `:173-186`); `:131-136` states "**No per-instance definition lookup is required: acceptance constrains instance identity, not definition identity (§2.4 item 3)**" |
| Therefore nothing in the loadout rule forbids two instances of one definition occupying two slots | derived from the above | — |
| The API delivers two elements for two instances of one definition, with **distinct** `relicId`s and the **same** `name` | authoritative test | `CollectionQueryServiceTests.cs:851-869` `ListRelics_ShouldReturnTwoElementsForTwoInstancesOfOneDefinition` — asserts `Count == 2`, ids `{relic_a, relic_b}`, and `Assert.All(relics, r => Assert.Equal("Berserker Core", r.Name))` |
| Ownership is instance-based | `DATABASE.md` §1 / `Relic.cs:25` | PK is `RelicInstanceId`; `Relic.cs:49` "the identity the battle loadout …" |

**Consequence for the implementation:** with two equipped instances of one definition, the map holds
**two distinct keys**, each carrying the **same** delivered name. A `RelicTriggered` naming either
instance resolves to that name — the correct outcome either way. There is **no instance/definition
cross-resolution**, because the key is the delivered `relicId` member on both sides.

**Honest limit:** the MVP starter grant mints **one instance per definition**
(`PlayerStarterGrantFactory.cs:273` `RelicInstanceId = $"relicinst_{Guid.NewGuid():N}"`, one per the
10 definition ids at `:128-137`), so duplicates do not arise from the starter grant today. That is a
*provisioning* fact, **not** a rule that forbids them — §2.4 item 3 is the rule, and it constrains
instances only. The implementation is correctly instance-keyed regardless, which is what makes it
safe for that case and for any future acquisition path.

### 3.3 A definition-id map is actively rejected by the tests

`SceneLifecycle.test.ts:3651-3652` asserts **both** non-keys:

```ts
expect(resolveInScene(ctx, 'relicinst_not_owned')).toBeNull();
expect(resolveInScene(ctx, 'relic-berserker-core')).toBeNull();
```

with the comment naming the drift `SIGNALR_PROTOCOL.md` §3.2.23 item 1 guards against. A
definition-keyed map would make the first assertion fail (the ten `relicinst_*` keys would not
exist) and the ten-name loop at `:3643-3645` would fail outright.

---

## 4. Name resolution — delivered data only, fail-closed

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Name comes from the delivered API read | **HOLDS** | `BattleScene.ts:1309` `await this.runtime.getRelics()` → `:1312` store → `:2160` read `?.name`. Port → `GameRuntime.getRelics()` → `ApiService.getRelics()` → `GET /api/relics` (`API_CONTRACTS.md` §5.4). No second API service, no direct HTTP. |
| No hard-coded Relic names | **HOLDS** | grep for all ten canonical names + `relicinst_` + `relic-` over `BattleScene.ts`: **zero hits**. Over `BattleEventPresenter.ts`: **one**, a doc comment (`:733` "Emergency Core"). Asserted mechanically at `SceneLifecycle.test.ts:3724-3756`, which strips comments then checks the ten names, the ten ids, `relicinst_`, `.trigger`, `.effectDefinition`, `conditionType`. |
| No raw `relicId` fallback | **HOLDS** | `relicDisplayName` returns `?? null` (`:2160`) — deliberately **not** the Card path's identity fallback (`cardDisplayName`, `:2174`). `describeEventCallout` returns `null` on `null` (`BattleEventPresenter.ts:683-685`). Asserted at `:702-706` and `SceneLifecycle.test.ts:3651`. |
| No technical trigger/effect text | **HOLDS** | Only `name` is read. The parse test at `BattleEventPresentation.test.ts:711-718` feeds `trigger`/`condition`/`effectDefinition` and asserts the parsed event is exactly `{ type, relicId }` — the members are not even reachable. |
| Unresolved definitions fail closed | **HOLDS** | Unknown id → `null` → no callout (`:688-698`); empty resolver → no callout (`:702-706`). E2E asserts the row is left untouched *and* that no scene Text anywhere carries the identity (`standalone-web-smoke.mjs:2778-2821`). |
| Failed `/api/relics` read does not crash the scene or the callout pipeline | **HOLDS** | `try/catch` at `BattleScene.ts:1308-1317` (empty map = "no name known"); `typeof getRelics !== 'function'` guard at `:1304`. Asserted with a **genuinely rejecting** port at `BattleEventPresentation.test.ts:1565-1579` (harness declares `relicsFailure` at `:75`, throws at `:125-126`) — no throw, no `RELIC:` line, no raw id. |

**Deviation from TASK-218A §10.1, and it is correct.** 218A sketched
`this.relicDefinitions.get(relicId)?.name ?? relicId` (identity fallback). The implementation fails
closed with `?? null` instead. TASK-218C's authoritative decisions require "no raw `relicId`
fallback" and "unresolved definitions fail closed", so the implementation is right and 218A's
sketch is superseded. It also matches the `BossSkillCast` precedent (`:611-613`), which declines a
technical identity rather than rendering it.

---

## 5. Ten-Relic coverage

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| All ten canonical Relics can be loaded | **HOLDS** | `SceneLifecycle.test.ts:3603-3614` `PROVISIONED_RELICS` — all ten with instance keys; `:3634-3653` loads them and asserts the port was called once. |
| All ten resolve to their delivered names | **HOLDS** | `:3643-3645` loops all ten asserting `resolveInScene(ctx, relic.relicId) === relic.name`. |
| No hard-coded ids required | **HOLDS** | The map is built from the delivered read; the source-scan test (`:3724`) forbids every canonical id/name in `BattleScene.ts` production code. |
| Compatible with 3–5 Relic loadouts | **HOLDS** | `GET /api/relics` returns the Player's **owned** instances, not the equipped ones, so every equipped instance is necessarily a key. Loadout size is therefore irrelevant to resolution; the resolution is keyed by the equipped instance the event names. The 3–5 rule itself is untouched (`RelicLoadoutService` is absent from the change set). |

**Not claimed:** that all ten must be equipped simultaneously. The ten are *owned*; a battle equips
3–5, and the callout names whichever equipped instance the server reports.

Coverage is **9 of 10 observable by design**: Burning Curse fires at `BattleStart`, which passes
`events: null` (`BattleStateService.cs:531-537`; `GAME_STATE.md` §2.0.5.2 item 2), so no
`RelicTriggered` is emitted for it. That is TASK-218A §2.4's documented boundary, unchanged here and
correctly not "fixed".

---

## 6. Lifecycle

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Initializes correctly | **HOLDS** | Field initializer `private relicDefinitions = new Map<string, RelicResponse>()` (`:411`); loaded from `create()` (`:442`). |
| Rebuilds rather than merges stale data | **HOLDS** | `this.relicDefinitions.clear()` **before** repopulating (`:1310`). Asserted at `SceneLifecycle.test.ts:3680-3699`: after a reload whose collection no longer contains Berserker Core, `relicinst_mana_crystal` resolves and `relicinst_berserker_core` is `null`. |
| Clears on `SHUTDOWN` | **HOLDS** | `shutdown()` `:584`; asserted via `harness.shutdownScene` at `:3667`. |
| Clears on `DESTROY` | **HOLDS** | Same handler is registered for both events (`:490-493`); asserted via `harness.destroyScene` at `:3676`. |
| No stale definitions across scene reuse | **HOLDS** | `:3674-3678` — a reused instance re-reads the collection on its next `create()` and resolves again. |
| Does not interfere with Card/Pet lifecycle | **HOLDS** | A separate map; `loadCardDefinitions`/`loadPetCatalog` bodies are **not in the diff**; `shutdown()`'s cache-clear block gained one line (`:584`) beside `cardDefinitions.clear()` and `petCatalog.clear()`; the teardown doc was updated to list the new cache (`:530-533`). |

**TASK-205 conventions:** the detach-before-attach registration (`:490-493`) is untouched, and no new
runtime subscription was added, so the "no stale listener after teardown" contract is unaffected.

**On the absent run guard — assessed directly, not waved through.** TASK-218A §3.5 recommended a
`ResultScene.rewardLoadRun`-style guard for the new async continuation. `loadRelicDefinitions` has
none — but **neither do** `loadCardDefinitions` (`:1210-1227`) or `loadPetCatalog` (`:1256-1282`),
which is the established `BattleScene` pattern this change was told to mirror. The concrete risk was
checked rather than assumed benign:

```text
A late continuation can only re-run `clear()` + `set(...)` against the map (BattleScene.ts:1310-1313).
It touches NO Phaser object — unlike loadCardDefinitions, whose continuation calls
renderCastControls() (:1222) and loadPetCatalog's, which calls renderPetHud/renderCastControls
(:1275-1276). So the new loader introduces no stale-write-to-destroyed-object hazard at all, and is
strictly safer than its siblings in the one respect that mattered for TASK-204.
The value it writes comes from the same port and the same owned collection, so a late write cannot
produce different content from a fresh one.
```

**Conclusion: lifecycle-safe, and consistent with TASK-205 as the codebase actually applies it.** The
missing guard is a pre-existing `BattleScene`-wide pattern, not a TASK-218B regression; the
recommendation in 218A §3.5 was not carried out, and on this evidence nothing is lost today.

---

## 7. Batch behaviour

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| At most one callout per batch | **HOLDS** | `selectBatchCallout` returns a single `EventCallout` (`:738-754`); `showCallout` writes one Text (`:2104-2125`). E2E `phase5c.oneSelectiveCalloutPerBatch` (`:2589-2593`) is **unchanged** and now covers a batch containing a Match, a Combo **and two Relic triggers** — a *harder* claim than before. |
| Existing priority ladder preserved | **HOLDS** | §2. |
| Existing last-delivered tie behaviour | **HOLDS** | `:748` unchanged; `:880-881` still asserts it. |
| No duplicate callouts | **HOLDS** | Scene-level `:1623-1630`: three `RelicTriggered` events in one batch yield exactly one `RELIC: ` line (`toEqual(['RELIC: Emergency Core'])`). |
| Multiple `RelicTriggered` do not render multiple callouts | **HOLDS** | Unit `:721-738` (three events → one, naming the last) and scene `:1581-1631`. |

The three-trigger test is **non-vacuous** on the mechanism: the harness records one entry per Text
and `setText` **mutates** that entry (`BattleEventPresentation.test.ts:134-146`), so a scene that
created a second callout Text would produce a two-element array and fail `toEqual([...])`. And a
selection that took the *first* rather than the last would fail the same assertion.

---

## 8. Test quality / non-vacuity

### 8.1 Would the tests fail if `relicId` were displayed instead of the delivered name? — **YES**

| Test | Assertion that would fail |
| --- | --- |
| `BattleEventPresentation.test.ts:670` | exact `toEqual({ message: 'RELIC: Berserker Core', color: '#7dd3fc', priority: 2 })` — an id-rendering implementation yields `RELIC: relicinst_a1` |
| `:674-675` | `expect(triggered?.message).not.toContain('relicinst_a1')` and `.not.toContain('relic-')` |
| `:1500-1514` (scene) | `toContain('RELIC: Berserker Core')` **and** `not.toContain('relicinst_berserker')` |
| `:1517-1537` | a second instance resolves independently; `not.toContain('relicinst_')` |
| `SceneLifecycle.test.ts:3643-3645` | the ten-name loop |
| `:3651` | `resolveInScene(ctx, 'relicinst_not_owned')` must be `null` — an identity fallback would return the id |
| E2E `standalone-web-smoke.mjs:2744-2745` | `callouts[0] === \`RELIC: ${relic.name}\`` **and** `!callouts[0].includes(relic.relicId)` |

The central assertion is **exact equality against the delivered name**, not a substring or a
presence check, so it cannot be satisfied by a raw identity.

### 8.2 Is the planted-leak protection genuine? — **YES, and it is demonstrated twice**

```text
Form 1 — behavioural (the strongest): the E2E unresolvable-Relic probe
  standalone-web-smoke.mjs:2778   pre-seeds the row to 'MATCH' before injecting
  :2784                            injects an unowned instance id
  :2806-2810                       asserts after === before, before === 'MATCH', and that the row was
                                   NOT turned into a RELIC line
  :2813-2816                       scans EVERY scene Text for the identity substring
  The comment at :2775-2777 states outright that a RELIC-prefix check alone would be vacuous —
  i.e. the author planted the leak and then defeated the vacuity. If the implementation fell back
  to the identity, `after` would become `RELIC: relicinst_not-owned-by-this-account` and all four
  conjuncts fail.

Form 2 — mechanical: SceneLifecycle.test.ts:3724-3756 strips comments from BattleScene.ts and
  asserts none of the ten names, none of the ten ids, 'relicinst_', '.trigger',
  '.effectDefinition', or 'conditionType' appear — while requiring
  /relicDefinitions\.get\(/ and /runtime\.getRelics\(/ to appear.
```

### 8.3 Do the tests miss player-facing behaviour in favour of internals?

**No overall, but this is where the defect sits.** The suite asserts the rendered callout line at
three levels — presenter (`describeEventCallout` return value), scene (`harness.texts` joined), and
E2E (the live callout row) — so internals are not the only evidence. `relicDisplayName` is called
directly in `SceneLifecycle.test.ts` (`:3628-3633`), which *is* internals-level, but it is
accompanied by the rendered-text assertions in the sibling spec.

### 8.4 **D-1 — the defect**

```text
Location   src/frontend/client/tests/SceneLifecycle.test.ts:3701-3722
Title      'holds no Relic definition when the collection read fails, and never throws'
Setup      createBattle([], new Error('GET /api/relics failed 500'))
             → createSceneHarness({ relics: [], relicsFailure: Error })   (:3619-3621)

WHY THE FAILURE NEVER HAPPENS
  SceneHarnessOptions                   :97-116   — no `relicsFailure` member
  createSceneHarness destructure        :119-126  — destructures state, withRuntime, battleState,
                                                    pets, relics, textsThrowWhenDestroyed only
  the port                              :341      — getRelics: vi.fn(async () => relics)
                                                    → always RESOLVES with []
  => the excess property is silently dropped; the read SUCCEEDS and returns an empty collection.
```

Because `relics` is `[]`, the map is empty, so every assertion in the test still holds
(`resolveInScene(...) === null`; no `RELIC:` in `harness.texts`; `create`/`emitBattleEvents` do not
throw). **The test passes — but it is not testing a failed read.** It is a duplicate of the empty-
collection case, and the dead `relicsFailure` member will mislead the next reader into believing the
rejection path is covered in this file.

**Severity: low.** It is test-only; the implementation's behaviour is correct and the rejected-read
path **is** genuinely exercised at `BattleEventPresentation.test.ts:1565-1579`, whose harness does
throw (`:75`, `:84`, `:125-126`). No required behaviour is unverified as a result. It is reported
because §8 of this audit is specifically about non-vacuity, and this test does not do what it says.

`tsc --noEmit` is clean and the conditional spread bypasses TypeScript's excess-property check, so
nothing else in the toolchain catches it.

---

## 9. E2E assertion quality

`standalone-web-smoke.mjs` — all diff hunks lie inside the phase-5c region
(`@@ -2370 … -2506` → `@@ +2502 … +2690`); nothing else in the file changed.

### 9.1 The allow-list

`isPlayerFacingCallout` (`:273-285`) explicitly allow-lists the Relic form:

```js
/^RELIC: \S/.test(callout) ||   // :280  — requires one non-space character after "RELIC: "
```

so `RELIC:` and `RELIC: ` (empty name) are rejected by the allow-list itself.

### 9.2 The independent rejection predicate

`carriesTechnicalIdentity` (`:299-311`) rejects, and is applied to both the real resolution's
callouts (`:2527-2531`) and the injected ones (`:2601`, `:2755`):

| Target | Rejected by | Status |
| --- | --- | --- |
| Raw instance id (`relicinst_…`) | `/relicinst/i` (`:301`) + exact equality (`:2744-2745`) + whole-scene scan (`:2813-2816`) | **REJECTED** |
| Relic definition id (`relic-berserker-core`) | `/relic-[a-z0-9-]+/i` (`:302`) + exact equality (`:2744`) | **REJECTED** |
| Technical event names (`RelicTriggered`, `PowerChanged`, …) | literal list (`:303-305`) | **REJECTED** (case-sensitive — see O-1) |
| Technical Trigger names (`OnMatchCount`, `OnCombo`, `OnHpBelow`, …) | `:306-308` | **REJECTED** (case-sensitive — see O-1) |
| Card ids | `/card-[a-z0-9-]+/i` (`:309`) | **REJECTED** |
| Arbitrary unrelated text (`hello world`) | allow-list only (`:2519`, `:2598-2601`) | **REJECTED** |
| `RELIC: <arbitrary non-space>` | not caught by form checks; caught by **exact equality** `callouts[0] === \`RELIC: ${relic.name}\`` (`:2744`) | **REJECTED in the Relic loop**; see O-2 for the real-resolution path |

The strongest Relic assertion is exact equality against the **name taken from the scene's own
delivered read**, plus `!callouts[0].includes(relic.relicId)` (`:2737-2748`).

### 9.3 Is the expected name hard-coded? — **No**

`RELIC_PROBE_SETUP` (`:547-554`) reads `s.relicDefinitions.values()` — the scene's own
`GET /api/relics`-backed map (`BattleScene.ts:1303-1312`) — and returns `{ relicId, name }` pairs.
`sceneRelics` is captured at `:2542`, **before** any injection, and gated by
`phase5cRelic.sceneLoadedTheDeliveredRelicRead` (`:2544-2552`: non-empty array, string `relicId`,
non-empty string `name`). No Relic name or id literal appears in any callout assertion.

### 9.4 Is the one-callout-per-batch assertion still meaningful? — **Yes, and it got harder**

`:2589-2593` is byte-identical to HEAD:2416 —
`injectedFeedback.callouts.includes('COMBO ×4') && injectedFeedback.callouts.length === 1`. The
injected batch (`:518-529`) now carries a `MatchCreated`, a `ComboChanged combo:4`, four damage
events, and **up to two `RelicTriggered`** appended at `:528`, i.e. **≥3 callout-eligible events**.
It therefore proves the p1 Combo beats the p2 Relic events *and* that they add no second line — a
strictly stronger claim than the pre-change two-eligible-event batch.

### 9.5 Was any assertion deleted or weakened?

**No record id was removed.** All seven HEAD phase-5c record ids are still present. Exactly **two
predicates** changed, both broadened:

| Record | Before (HEAD) | After | Assessment |
| --- | --- | --- | --- |
| `phase5c.aRealResolutionReachesThePlayer` (`:2500-2508`) | `some(c => /^(MATCH\|COMBO ×\d+)$/.test(c))` | `some(c => isPlayerFacingCallout(c))` | Broader form set; **narrower** for `COMBO ×1` (the old `\d+` accepted it). Justified — a real resolution may now emit `RELIC:`. |
| `phase5c.comboCalloutUsesTheDeliveredValue` (`:2517-2521`) | `every(c => c === 'MATCH' \|\| /^COMBO ×[2-9]\d*$/.test(c))` | `every(c => isPlayerFacingCallout(c))` | **O-2.** Material loosening; compensated by the new `:2527-2531` rejection record. |

**On "do not broaden merely to make it pass":** the broadening here is not cosmetic. Before
TASK-218B, a Relic trigger produced no callout, so "MATCH or COMBO only" was a *complete*
description of a real resolution. After it, a correct implementation legitimately emits
`RELIC: <name>` whenever a Relic fires in a real swap, so the old predicate would fail on **correct**
behaviour. The widening is required, and it is offset by a **new, independent rejection assertion**
(`phase5c.noCalloutLeaksATechnicalIdentity`, `:2527-2531`) that had no predecessor. The strictly
Relic-specific assertion was **not** broadened: it is exact equality.

---

## 10. Regression

**No existing presentation behaviour changed.** The `describeEventCallout` switch gained one arm
between `PetSkillCast` and `BossSkillCast` (`:679-690`); every other arm is unchanged, including its
message text, colour, and priority.

| Surface | Unchanged? | Evidence |
| --- | --- | --- |
| Combo | **YES** | `:659-666`; test `:745` (`combo 1` → `null`) and `:748-752` (`COMBO ×4`, priority 1) both still pass |
| Card cast | **YES** | `:667-672`; tests `:762` (`CARD: Heal`) and `:771` (unknown id → `CARD: card-unknown`) |
| Pet Skill | **YES** | `:673-678`; `:779` (`PET SKILL: card-inferno`) |
| Boss | **YES** | `:691-696`; `:788` (`BOSS SKILL`, priority 3, no skillId leak) |
| `PassiveTriggered` | **YES** | `:697-702`; `:834` |
| `PassiveCharged` | **YES** | `:703-708`; `:805` (player) and `:819` (boss) |
| Match | **YES** | `:709-714`; `:854` (priority 6) |
| Everything else → `null` | **YES** | `:715-717`; the "no callout" table `:637-657` retains GemMatched / CascadeCreated / DamageCalculated / DamageDealt / DamageTaken / PowerChanged — **only the `RelicTriggered` row was removed**, which is the deliberate, anticipated edit (TASK-218A §10.5 anticipated exactly this). |

**Suite result:** the full client suite was executed for this audit —
**21 files, 886 tests, all passing** (`vitest run`, exit code 0). `tsc --noEmit` exit code 0 under
`strict`, `noUnusedLocals`, `noUnusedParameters` with `include: ["src", "tests"]`. No sibling spec
calls the changed functions with two arguments (26 call sites, all three-argument).

---

## 11. Scope boundary

**Verified: no leakage.**

```text
git status --porcelain → exactly 5 modified paths, all under src/frontend/client:
  src/frontend/client/scripts/standalone-web-smoke.mjs
  src/frontend/client/src/game/scenes/BattleEventPresenter.ts
  src/frontend/client/src/game/scenes/BattleScene.ts
  src/frontend/client/tests/BattleEventPresentation.test.ts
  src/frontend/client/tests/SceneLifecycle.test.ts
```

| Must not change | Changed? | Evidence |
| --- | --- | --- |
| Backend | **NO** | Zero `.cs` files in the change set |
| SignalR protocol | **NO** | `BattleEventWireProjection.cs`, `BattleHub.cs` untouched; the E2E still injects the existing `{ type, relicId }` shape |
| API contracts | **NO** | No controller, response, or DTO file touched; `RelicResponse` is consumed as delivered |
| Database / schema | **NO** | No migration touched |
| Relic mechanics | **NO** | `RelicResolver.cs`, `RelicDefinition.cs`, `EquippedRelicContent.cs` untouched |
| Relic triggers / effects | **NO** | No trigger evaluated, no magnitude read, no `effectDefinition` access added |
| 3–5 loadout rule | **NO** | `RelicLoadoutService.cs` / `RelicLoadoutValidation.cs` untouched |
| Ownership / acquisition | **NO** | `PlayerStarterGrantFactory.cs`, `RelicRepository.cs` untouched |
| Signature Skill | **NO** | `signatureSkills`/`loadPetCatalog` bodies unchanged |
| Passive effects | **NO** | No passive path touched |
| `GameRuntimeEvents.ts` (forbidden by 218A §10.1) | **NO** | Not in the change set; `getRelics()` already existed on the port |
| New module / scene / queue / HUD region / icon set | **NO** | The change adds one map, one loader, one resolver, one callout arm |

**Bookkeeping observation (O-7):** there is **no TASK-218B task record** in `tasks/completed/`
(only `TASK-218A-…`, and the implementation is uncommitted). `TASK_LIFECYCLE.md` §DONE requires
"File location: `tasks/completed/`". This is a process gap, not a code defect.

---

## 12. Known phase-6d race — out of scope, and untouched

The `CAST_CONTROL_CAPTIONS` race is treated as **out of scope** and was **not** fixed.

| Element | Changed by TASK-218B? | Evidence |
| --- | --- | --- |
| `loadCardDefinitions()` | **NO** | Absent from the diff; body `:1210-1227` unchanged |
| `loadPetCatalog()` | **NO** | Absent from the diff; body `:1256-1282` unchanged |
| `renderCastControls()` | **NO** | Absent from the diff |
| Their ordering / waiting | **NO** | `create()` gained one **inserted** line at `:442`, *after* `:440-441`. No call was reordered, awaited, or guarded. The new call is fire-and-forget `void`, like its siblings, and does not await them or they it. |
| E2E caption waits | **NO** | Every smoke-script hunk is inside the phase-5c region (old 2370–2506). `CAST_CONTROL_CAPTIONS` does not appear in the diff at all. |

**Classification: separate follow-up candidate only.** The race is a pre-existing phase-6d
`BattleScene` ordering concern. TASK-218B neither introduced, altered, nor worsened it — the new
loader does **not** call `renderCastControls` (unlike both siblings), so it adds no participant to
that ordering at all.

---

## 13. Findings

### 13.1 Defect

```text
D-1  TEST NON-VACUITY — SceneLifecycle.test.ts:3701-3722
     The failing-/api/relics test does not fail the read (no `relicsFailure` in
     SceneHarnessOptions :97-116 / destructure :119-126; `getRelics` :341 always resolves).
     It duplicates the empty-collection case and passes for the wrong reason.
     Severity      low — test-only; the behaviour is genuinely covered at
                   BattleEventPresentation.test.ts:1565-1579
     Not a defect in: production code, contracts, mechanics, E2E
```

### 13.2 Observations (recorded, not blocking)

```text
O-1  E2E rejection predicate is case-sensitive where the id arms are not.
     standalone-web-smoke.mjs:303-305 and :306-308 carry no `i` flag (while :301/:302/:309 do),
     so a lowercase `RELIC: relictriggered` passes the allow-list and every leak arm — caught only
     by the exact-equality at :2744, and only in the phase-5c-relic loop, not the real-resolution
     path (:2519/:2529). A bare non-conforming instance id (e.g. a GUID) shown as `RELIC: <guid>`
     likewise evades :2519/:2529. Unreachable from the production presenter, which builds
     `RELIC: ${name}` from the delivered name only (BattleEventPresenter.ts:686). Hardening only.

O-2  phase5c.comboCalloutUsesTheDeliveredValue (:2517-2521) is now form-only.
     The widening is NECESSARY and compensated (:2527-2531), but the record's name no longer
     describes what it checks, and no exact-equality check covers a Relic line on the
     real-resolution path.

O-3  phase5c.oneSelectiveCalloutPerBatch proves one distinct callout VALUE, not one invocation —
     the scene owns a single callout Text (BattleScene.ts:347, created :817), so two showCallout
     calls in one synchronous batch would leave only the last value visible. PRE-EXISTING (identical
     in HEAD); the fixture only gained events. The same applies to the stale-text caveat
     (drainFeedback :408 destroys only feedbackLayer children, :415 — not calloutText) — which the
     NEW unowned probe explicitly works around by pre-seeding 'MATCH' (:2778), while the older
     record does not.

O-4  The E2E fixture ids are read from the scene's own map (RELIC_PROBE_SETUP :547-554), so a
     wrongly-keyed map would produce a self-consistent fixture. The instance-id-class claim rests on
     the unit tests (SceneLifecycle.test.ts:3643-3652) and the backend contracts (§3.1), not on the
     smoke script — which is why §3 of this audit was verified from source.

O-5  two E2E comments overstate their rationale: :504-506 and :2587-2588 say the Relic events "sit
     after the Combo" so the ladder "still selects COMBO ×4". Ordering is not the reason — priority
     is (:748; ties only go to the LAST equal). The append position IS load-bearing for a different
     reason: presentCombatFeedback resets `previousDamage` on unhandled types
     (BattleScene.ts:1959-1968), so inserting a RelicTriggered between a DamageDealt/DamageTaken
     pair would break the pair collapse and fail the unchanged one-floater assertion (:2610-2618).

O-6  `relicDisplayName` uses `?? null` (:2160), so a delivered empty-string `name` would render
     `RELIC: ` rather than nothing. The E2E allow-list (`/^RELIC: \S/`, :280) would reject it, and
     `phase5cRelic.sceneLoadedTheDeliveredRelicRead` (:2549) requires a non-empty name — so the
     player-facing surface is guarded. No provisioned row has an empty name. Hardening only.

O-7  No TASK-218B task record exists in tasks/completed/ (see §11).
```

---

## 14. Recommended next action — the smallest follow-up

**MODIFY is test-only.** The production implementation is verified correct and needs no change.

```text
FOLLOW-UP (smallest): make the new Relic lifecycle test non-vacuous, and restore the
combo-callout record's descriptive integrity.

  1. src/frontend/client/tests/SceneLifecycle.test.ts
     EITHER  add `relicsFailure?: Error` to SceneHarnessOptions (:97-116) and honour it in the
             port (`getRelics: vi.fn(async () => { if (relicsFailure) throw relicsFailure;
             return relics; })`, :341) — mirroring the BattleEventPresentation harness (:75, :84, :125-126)
     OR      delete the dead `relicsFailure` parameter (:3619-3621) and re-point the test
             (:3701) at the empty-collection case it actually exercises, citing
             BattleEventPresentation.test.ts:1565-1579 as the rejection-path coverage.
     Result: the test either does what its name says, or stops claiming to.

  2. src/frontend/client/scripts/standalone-web-smoke.mjs (optional, same edit session)
     Rename `phase5c.comboCalloutUsesTheDeliveredValue` (:2517) to describe what it now checks
     (a player-facing FORM over the real resolution), or add an exact-equality check on a
     real-resolution Relic line. Do NOT narrow it back — the widening is required.

EXACT BOUNDARY — the follow-up must NOT:
  • touch any production file (BattleScene.ts, BattleEventPresenter.ts)
  • change the callout format, priority 2, the tie rule, or the one-callout-per-batch limit
  • narrow the E2E allow-list back to MATCH|COMBO, or weaken any Relic assertion
  • touch the backend, SignalR, API contracts, schema, Relic mechanics, loadout, or ownership
  • touch the phase-6d CAST_CONTROL_CAPTIONS race (separate candidate; §12)
```

**Recommended next task:** `TASK-218D — make the TASK-218B Relic lifecycle test non-vacuous and
restore the combo-callout record's descriptive integrity (test-only)`.
Deferred candidates, explicitly **not** part of it: the phase-6d race (§12), O-1's case-sensitivity
hardening, and TASK-218A's F-1 in-process `_relicConfiguration` recovery gap.

---

## 15. Method, evidence, and limits

- **Read, not inherited.** Every load-bearing claim carries a `file:line` from the working copy.
  TASK-218A's findings were re-verified against the code rather than restated; where 218A's
  *recommendation* differed from the implementation (`relicDisplayName` fallback, the run guard),
  the difference is recorded and assessed in §4 and §6 rather than assumed to be a defect.
- **Executed for this audit:**
  ```text
  cd src/frontend/client
  .\node_modules\.bin\vitest.CMD run      → 21 files, 886 tests, ALL PASSING (exit 0)
  .\node_modules\.bin\tsc.CMD --noEmit    → exit 0 (strict; include: src, tests)
  ```
  The `[exit code: 1]` on the first targeted two-file run was the Vitest v3 deprecation notice for
  the `basic` reporter on stderr, not a test failure (both files reported passing).
- **NOT executed, and not claimed:**
  ```text
  standalone-web-smoke.mjs   — requires PostgreSQL + Redis + GameServer.Api + a headless browser;
                               the E2E is reviewed statically only
  backend (dotnet test)      — no backend file is in the change set; backend source and tests were
                               read as authority for the identity domain, not run
  ```
- **An independent second reviewer** audited the E2E script's assertion surfaces in isolation; its
  findings (the case-sensitivity gap, the two broadened predicates, the single-Text caveat, the
  fixture circularity) are recorded as O-1–O-5 and were re-verified against the file here.
- **Scope of this record:** audit only. The only file created is this document. No production, test,
  script, or documentation file was modified, and no Git state was staged, committed, reset, or
  cleaned (`git status` still reports the same five modified paths plus this record).

---

TASK-218C AUDIT COMPLETE

Decision: MODIFY — implementation correct on every enumerated criterion; the required change is
**test-only** (D-1, §8.4, §14). No production defect, no contract defect, no mechanic defect, no
scope leakage.

Event source: **server-authoritative and unchanged.** `RelicResolver.cs:314`/`:381` →
`BattleEventWireProjection.cs:553-556` → `BattleHub` `ReceiveEvents` → `BattleScene.presentEventBatch`
(`:1867-1889`). The client added a **name lookup only** — no evaluation, no second event or
presentation path, no new listener, queue, or Text. `parseInBattleEvent`/`formatInBattleEvent` and
`presentCombatFeedback` are untouched; the sole delta in the chain is the `resolveRelicName` argument
at `:1885`.

Priority: **exactly 2** (`BattleEventPresenter.ts:595`, asserted by exact `toEqual(… priority: 2)`
at `BattleEventPresentation.test.ts:674`). The full ladder is preserved unchanged — Combo 1 (`:593`),
cast 2 (`:594`), Relic 2 (`:595`), Boss 3 (`:596`), PassiveTriggered 4 (`:597`), PassiveCharged 5
(`:598`), Match 6 (`:599`) — and **no existing constant was renumbered**. Combo still outranks Relic
(`:907`); Relic ties Card/Pet cast (`:920-924`); Match remains lowest (`:854`). Relic vs
Passive 4/5 is not directly batched but is provable from the individually pinned constants (§2.2).

Tie behavior: **existing last-delivered rule intact** — `candidate.priority <= selected.priority`
(`:748`) is byte-identical to HEAD, and the pre-existing two-casts assertion (`:880-881`) still
passes. A Relic followed by a cast in one batch resolves to the cast (`:925`).

Relic identity: **owned INSTANCE identity on both sides — verified from source, not assumed.**
Emission carries `equipped.InstanceIdentity.Value` = `RelicInstanceId` (`RelicResolver.cs:314`,
`:381`; `RelicWireProjectionTests.cs:54`); the read projects `Relic.RelicInstanceId` into `relicId`
(`CollectionQueryService.cs:335`, `CollectionResponses.cs:301`/`:315`;
`CollectionEndpointTests.cs:1026-1028`); the scene keys on that same member
(`BattleScene.ts:1312` → `:2160`). **Two owned instances of one definition, both equipped, is a real
case**: the loadout rule constrains instance identity only (`RelicLoadoutService.cs:120-136`,
`RELIC_RULES.md` §2.4 item 3) and the API is proven to deliver two elements with distinct `relicId`s
and the same name (`CollectionQueryServiceTests.cs:851-869`). The map then holds two distinct keys
with that same name, so either instance resolves correctly — **no instance/definition
cross-resolution**. The starter grant happens to mint one instance per definition
(`PlayerStarterGrantFactory.cs:273`), which is a provisioning fact, not a rule; the implementation is
instance-keyed regardless. A definition-keyed map is actively rejected by
`SceneLifecycle.test.ts:3643-3652`.

Name source: **delivered `GET /api/relics` `name` only, fail-closed.** Read through the existing port
(`BattleScene.ts:1309` → `GameRuntimePort.getRelics()` → `ApiService.getRelics()`), stored as
delivered, read as `?.name ?? null` (`:2160`) — a deliberate **fail-closed** choice that supersedes
TASK-218A §10.1's identity fallback and matches TASK-218C's "no raw `relicId` fallback" decision and
the `BossSkillCast` precedent. **No hard-coded name or id** (grep over `BattleScene.ts`: zero hits; a
mechanical comment-stripped source scan at `SceneLifecycle.test.ts:3724-3756`). No `trigger`,
`condition`, or `effectDefinition` is ever read. An unresolved id produces **no callout** rather
than a raw instance id (`BattleEventPresenter.ts:683-685`); a rejected read leaves the map empty and
crashes nothing (`BattleScene.ts:1308-1317`, tested with a genuinely throwing port at
`BattleEventPresentation.test.ts:1565-1579`).

10-Relic coverage: **all ten load and resolve to their delivered names**, instance-keyed, with no
hard-coded ids (`SceneLifecycle.test.ts:3588-3599`, `:3634-3653`). A 3–5 loadout is unaffected: the
read returns the **owned** instances, so every equipped instance is necessarily a key. All ten are
owned; the audit does **not** interpret this as requiring all ten equipped. 9 of 10 are observable
by design — Burning Curse fires at `BattleStart`, which passes `events: null`
(`BattleStateService.cs:531-537`), a documented boundary left correctly unfixed.

Batch behavior: **at most one callout per batch, preserved.** `selectBatchCallout` returns one
(`:738-754`); one callout Text and one `showCallout` (`BattleScene.ts:2104-2125`). Multiple
`RelicTriggered` events yield exactly one line, naming the last (`BattleEventPresentation.test.ts:721-737`;
scene-level `:1623-1630`, `toEqual(['RELIC: Emergency Core'])`) — non-vacuous, because `setText`
mutates the single recorded Text entry (`:134-146`) and a first-wins selection would fail. The E2E
`phase5c.oneSelectiveCalloutPerBatch` predicate is **unchanged** and now covers a batch with a Match,
a Combo **and two Relic triggers** — a stricter claim than before.

Lifecycle: **safe and consistent with TASK-205 as the codebase applies it.** Initialized at `:411`;
loaded from `create()` (`:442`); **rebuilt not merged** (`clear()` before repopulating, `:1310`);
cleared on `SHUTDOWN` and `DESTROY` via the single detach-before-attach handler (`:490-493`, `:584`);
no stale data across reuse (`SceneLifecycle.test.ts:3655-3678`, `:3680-3699`); Card/Pet loaders
byte-unchanged and their caches cleared beside it. The absent `rewardLoadRun`-style run guard is a
**pre-existing `BattleScene`-wide pattern** (neither `loadCardDefinitions` nor `loadPetCatalog` has
one), and the new loader is **strictly safer** than its siblings in the one respect that mattered for
TASK-204: its continuation writes only to a Map, never to a Phaser object, so it cannot produce a
stale-write-to-destroyed-object hazard.

Test quality: **substantively meaningful, with one non-vacuity defect (D-1).** The tests **would**
fail if `relicId` were displayed instead of the delivered name — the central assertion is exact
equality against the delivered name (`:670`), reinforced by `not.toContain('relicinst_…')` at
`:674-675`, the ten-name loop at `:3643-3645`, the definition-id-is-not-a-key assertion at `:3652`,
and E2E exact
equality at `:2744-2745`. The planted-leak protection is genuine and demonstrated behaviourally —
the E2E unresolvable probe pre-seeds `MATCH`, injects an unowned id, asserts the row is untouched,
and scans **every** scene Text for the identity (`standalone-web-smoke.mjs:2778-2821`), explicitly
defeating the RELIC-prefix vacuity its own comment describes. Player-facing behaviour is asserted at
three levels (presenter, scene-rendered text, live E2E row), not only against internals. **D-1:**
`SceneLifecycle.test.ts:3701` claims to test a failing `/api/relics` read but never fails it
(`SceneHarnessOptions` `:97-116` lacks `relicsFailure`; `getRelics` `:341` always resolves), so it
duplicates the empty-collection case and passes for the wrong reason — low severity, test-only, with
the rejection path genuinely covered at `BattleEventPresentation.test.ts:1565-1579`.

E2E quality: **strong, allow-listed, and not broadened to pass.** `/^RELIC: \S/` explicitly
allow-lists the form (`:280`); the expected name comes from the scene's own delivered read
(`RELIC_PROBE_SETUP` `:547-554`, gated non-vacuous at `:2544-2552`), never hard-coded; the fixture
`relicId`s are real owned instance ids. Raw instance ids, definition ids, event-type names, technical
Trigger names, Card ids, empty/arbitrary text, and an unresolvable instance are all rejected by
independent predicates (`:299-311`, `:2519`, `:2529-2531`, `:2598-2601`, `:2737-2748`, `:2753-2757`,
`:2799-2821`). **No record id was deleted**, and the one-callout-per-batch assertion remains
meaningful and is now harder to satisfy. Two predicates were broadened — necessarily, because a real
resolution may now emit `RELIC:` — and one is offset by a new rejection record (O-2, O-1;
`:2517-2521`, `:2500-2508`). `MATCH`/`COMBO` verification was not replaced: `COMBO ×4` is still
explicitly required (`:2591`) and `COMBO ×1` still forbidden (`:276`).

Existing callout regression: **NONE.** One switch arm added; every other arm's message, colour, and
priority unchanged. Combo, Card cast, Pet Skill, Boss, `PassiveTriggered`, `PassiveCharged`, and
Match all retain their assertions and their presentation. The only table edit is the deliberate,
pre-announced removal of the `RelicTriggered` "no callout" row (TASK-218A §10.5), replaced by
positive coverage. Full client suite: **886 tests across 21 files, all passing**; `tsc --noEmit`
clean.

Phase-6d race: **out of scope, untouched, classified as a separate follow-up candidate.**
`loadCardDefinitions()`, `loadPetCatalog()`, and `renderCastControls()` are absent from the diff;
their ordering and waiting are unchanged (`create()` gained one inserted fire-and-forget line at
`:442`, after `:440-441`, with no reorder, await, or guard); and every E2E hunk lies inside the
phase-5c region, with `CAST_CONTROL_CAPTIONS` not appearing in the diff at all. The new loader does
not call `renderCastControls`, so it adds no participant to that ordering. **Not fixed.**

Scope integrity: **intact.** `git status --porcelain` reports exactly the five client files —
no backend, SignalR, API, schema, migration, Relic-mechanic, trigger/effect, 3–5 loadout, ownership,
Signature Skill, or Passive change; `GameRuntimeEvents.ts` correctly untouched; no new module, scene,
queue, HUD region, or icon set. Bookkeeping gap recorded: **no TASK-218B task record exists** in
`tasks/completed/`.

Recommended next task: `TASK-218D — make the TASK-218B Relic lifecycle test non-vacuous and restore
the combo-callout record's descriptive integrity (test-only)`: honour `relicsFailure` in
`SceneLifecycle.test.ts`'s harness (mirroring `BattleEventPresentation.test.ts:75`, `:84`,
`:125-126`) or remove
the dead parameter and cite the real rejection coverage; optionally rename
`phase5c.comboCalloutUsesTheDeliveredValue`. Boundary: test files only — no production file, no
callout format, priority, tie rule, batch limit, backend, contract, mechanic, or E2E allow-list
change, and the phase-6d race stays a separate candidate.

Implementation changes: NONE
Test changes: NONE