# TASK-213 — Post-TASK-212A-1 Product & Gameplay Gap Audit

```text
Task ID:            TASK-213
Type:               AUDIT (evidence-based product/gameplay gap classification;
                    no gameplay, UI, backend, contract, or documentation change)
Status:             DONE
Risk:               NONE (read-only audit; the only file created is this record)
Priority:           HIGH (determines whether TASK-212B runs next, and what runs
                    instead if it does not)
Primary Agent:      review / product-audit
Evidence base:      current working tree (src/, tests/, docs/, tasks/, scripts/)
                    + a live browser E2E run against the real stack
Model:              DeepSeek Harness agent
```

**Scope discipline.** This task audits only. No production code, test, migration,
contract, ADR, or `docs/` file was modified. No temporary instrumentation was
added. The single file created is this record. TASK-207 / TASK-208 / TASK-208A /
TASK-210 / TASK-211 / TASK-212 / TASK-212A / TASK-212A-1 are treated as the
shipped baseline; where this audit disagrees with a recorded claim, the
disagreement is **reported** (`AGENTS.md` §16) and the completed record is **not**
edited (`tasks/TASK_LIFECYCLE.md` §3).

**No implementation is performed here.** TASK-213, TASK-214, TASK-215, TASK-217,
TASK-218, TASK-219, TASK-212B and every `TASK-220+` id used below are *proposals*,
not authorization. TASK-212B and TASK-219 are named because the task statement
asks for a verdict on them; naming them is not authorization.

**Audit-local gap IDs.** New findings use an audit-local `N-nn` prefix. Prior IDs
are cross-referenced explicitly (`A-01…A-20` = TASK-212); `G1…G15` = TASK-207;
`NG-01…NG-29` = TASK-211 audit. Nothing is silently renumbered.

---

## 0. Verification Performed (audit evidence base)

```text
git status --porcelain                 37 modified tracked files + 11 pre-existing
                                       untracked entries (2 source/test files
                                       added by TASK-212A-1, 9 completed task
                                       records) — 12 untracked once this record
                                       exists. HEAD = afe5b14 and unchanged by
                                       this audit
git diff --check                       PASS (exit 0)
npx tsc --noEmit (client)              PASS (exit 0, no output)
npx vitest run   (client)              PASS — 21 files / 857 tests
Live stack preconditions               PostgreSQL listening on 5433 and Redis on
                                       6379 were already up
Started for this audit                 backend  `dotnet run --project
                                       src/backend/GameServer.Api/GameServer.Api.csproj`
                                       (ASPNETCORE_ENVIRONMENT=Development,
                                       DevelopmentAuthentication__Enabled=true;
                                       /health -> "Healthy" at :5000)
                                       client   `npm run dev` (200 at :5173)
Browser E2E  npm run verify:e2e:smoke   PASS — RUN 1: 138 checks / 0 failures;
                                       RUN 2: 138 checks / 0 failures;
                                       "ALL SMOKE TEST RUNS PASSED CLEANLY
                                       (NO FLAKINESS)"
                                       (Microsoft Edge headless, real backend +
                                       PostgreSQL + Redis + SignalR, two real
                                       battles played to terminal resolution:
                                       10 swaps / 7 s and 9 swaps / 7 s)
Post-run cleanup                       both dev servers stopped; 0 listening
                                       sockets on 5000/5173
Artifacts written                      only the gitignored `**/*-shots/` PNGs
                                       (`.gitignore:19`); no tracked file touched
```

**Post-run `git status` was re-read after the E2E**: the 37 modified tracked
files and the 2 untracked source/test files are byte-identical to the pre-run
state (`git status --porcelain -- src tests docs` minus this record = 39 entries,
all pre-existing). This record is the only file this audit added.

**Backend unit suites (`dotnet test`) were NOT re-run** by this audit: no backend
file was changed by TASK-212A-1's *client* half, and this audit changed no
backend file. Backend claims below are verified by **direct source, contract and
test reading**, plus the live E2E for the paths it exercises.

**Method.** Each headline claim was independently re-derived from source, from the
authoritative documents, or from the browser — not from the prior records. The
five audit subjects (TASK-212A-1 post-condition; TASK-212B readiness; Pet
Passive; Signature Skill; content reachability) were each inspected by a
dedicated read-only pass plus direct reads by this audit; every claim that rests
on an inference rather than an artefact is marked `UNVERIFIED`.

---

## 1. Executive Decision

```text
TASK-212A-1's post-condition HOLDS.        §3
TASK-212B:  DEFER.                         §4
Pet Passive: D (deferred by explicit       §5
             Product Owner decision) for
             the EFFECT layer; the
             mechanism and the presentation
             are implemented.
Signature Skill: CONTRACT DRIFT — a        §6
             data-contract conflict between
             two authoritative documents,
             not an implementation bug.
Recommended immediate next task: TASK-213  §11
             (content reachability decision)
Next implementation task after it: TASK-218
             (Relic trigger callout), which
             may run in parallel.
```

**Why `DEFER` and not `PROCEED`.** TASK-212B cannot be implemented correctly
today for three independent reasons, each of which is a recorded stop condition
rather than an effort estimate:

1. **Its only motivating content is unreachable.** The single provisioned
   `CardCost` source is Emergency Core, which is not in the starter grant
   (`PlayerStarterGrantFactory.cs:81-84,90-95`) and cannot be acquired by any
   production path, so `PetState.CardCostModifiers[]` (`PetState.cs:637`) is
   empty in **every battle a real account can start** (§4.1). Implementing cost
   now would expose a contract whose only consumer does not exist.
2. **A client that must show a cost is instructed to read a source that is
   forbidden and, for the spent value, insufficient.** `SIGNALR_PROTOCOL.md`
   §3.2.20 item 2 (`:1098-1100`) tells the client to "read the Card's
   definition" for the spent Cost; §4 item 15 (`:1524-1527`) forbids
   reconstructing a Card cost "from a Card's definition"; §4.3 item 15
   (`:1987-1992`) forbids any client-side cost/affordability/legality
   computation; and §5.3 does not expose `powerCost` at all
   (`API_CONTRACTS.md:867`). This is an **intra-document conflict** — `AGENTS.md`
   §2's precedence rule cannot resolve it, because both sentences are in the same
   document (N-05).
3. **Its player-facing payoff is already partly obtainable for free.** The cast
   path already produces authoritative rejection semantics (`CARD_NOT_IN_LOADOUT`,
   `INSUFFICIENT_POWER`, `INVALID_CARD`, `CARD_CAST_ALREADY_USED_THIS_TURN`) and
   the client already renders them (`BattleScene.ts:1658-1661`). The
   decision-free half of the affordability concern is *presentation of the
   server's own answer*, which belongs to a presentation task, not to a wire
   contract (§4.4).

**Why `DEFER` and not `REPLACE`.** "REPLACE" would imply TASK-212B is the wrong
*task*; it is not — it is the right task at the wrong time. Its blocking
decisions are named, its owner document is identified, and it becomes correct the
moment content is reachable and the intra-document conflict is ratified. What
this audit does assert is that **another task must take the next slot** (§10/§11).

---

## 2. Current Product / Gameplay State

1. **The loop is complete and green.** Auth → Main Menu → Lobby → loadout +
   Boss → battle → Result → PLAY AGAIN / MAIN MENU runs end to end against the
   real stack; this audit's own browser run passed **138 checks × 2 runs with 0
   failures**, including two real battles played to terminal resolution. Client
   `tsc` and 21 files / 857 tests are clean.
2. **The comprehension gap TASK-212A-1 targeted is closed, and this audit
   confirms it live**, not from the record:
   ```text
   PASS phase4.cardRowsStateWhatTheyChange
        ["Heal 20% Max HP","Power 25","Shield 20% Max HP"]
   PASS phase4.relicRowsStateTriggerConditionAndEffect
        ["OnCombo ComboAtLeast 3 | Crit 10 percentage points (Pet, NextAttack)",
         "OnMatchCount MatchCountAtLeast 4 | Power 10 (Pet, Immediate)",
         "OnMatchCount MatchCountAtLeast 3 | ATK 5% (Pet, Battle)"]
   ```
3. **The loadout is still a forced permutation, confirmed at runtime.** The E2E
   reports `phase4.starterGrantLoaded — {"pets":1,"cards":3,"relics":3}` against
   requirements of exactly 1 Pet, exactly 3 Basic Cards and 3–5 Relics
   (`API_CONTRACTS.md:398-414`), and `"cards":3,"relics":3` is also what the
   preserved-loadout restore carries. So TASK-212A-1 improved *comprehension*
   without changing a single *decision* — exactly as its own record states
   (`TASK-212A-1:584-587`).
4. **Three of the four documented Pet-progression axes do not exist as play.**
   Build diversity (`GDD.md:61-62`) is dead; the Pet Passive's effect never
   happens; Tier/Star progression has no implementation path. All three are
   `AGENTS.md` §4 conflicts against `MVP_SCOPE.md` §1, not implementation
   backlog.
5. **The one genuinely free decision is the Boss**, and it is now five real
   choices (`phase4.fiveBossesRendered`).
6. **Maturity is unchanged from TASK-212's verdict: vertical-slice-complete,
   presentation-mature, decision-poor.** What changed in TASK-212A-1 is that the
   *legibility* debt on the loadout screen was paid; what did **not** change is
   that every remaining high-impact item is gated on a Product Owner decision.
   That is why this audit re-ranks a decision task to the next slot (§10/§11).

---

## 3. TASK-212A-1 Post-Condition Assessment

Verdict: **HOLDS.** Each required post-condition was re-verified against the
current working tree.

### 3.1 Card effect content is reachable at the loadout decision point — **HOLDS**

| Layer | Evidence |
|---|---|
| Persistence | `CardRepository.ListUnlockedAsync` already joined `PlayerUnlockedCard → CardDefinition`; **no repository change** (`CardRepository.cs:47-63`) |
| Application | `CardCollectionItem.cs` gains `EffectDefinition`; `CollectionQueryService.cs:214-232` copies `definition.EffectDefinition` — a field copy, no composition |
| Api | `CardResponse` gains `effectDefinition`; `CardEffectResponse.From` (`CollectionResponses.cs`) maps `EffectType`/`ValueType`/`Value`/`Duration`/`Scope` with `JsonIgnoreCondition.WhenWritingNull` for the three present-iff members |
| Contract | `API_CONTRACTS.md:3-19` (v1.18) + §5.3; live body asserted by the E2E (`phase4.renderedContentMatchesTheDeliveredResponse`) |
| Client | `CollectionModels.ts`; `ContentEffectFormat.ts` (new, 320 lines, plain functions); consumed at `LobbyScene.ts:1312` and `CollectionViewerScene.ts:791` — **and nowhere else** (grep: 12 matches, all these sites) |

### 3.2 Relic trigger/condition/effect content is reachable — **HOLDS**

`RelicCollectionItem.cs` gains `Trigger` / `Condition` / `EffectDefinition`;
`CollectionQueryService.cs:288-331` replaces the name-only dictionary with a
definition dictionary and copies three fields. The live E2E confirms the
nullable-condition spelling and the trigger/condition/effect text for all three
starter Relics. `definitionId` remains unexposed.

### 3.3 No cost/affordability contract was accidentally introduced — **HOLDS**

* `powerCost` is still listed as not-exposed (`API_CONTRACTS.md:867`); no
  cost/affordability/legality member exists on either response.
* The client source contains **zero** cost computations: every
  `powerCost`/`EffectiveCardCost`/`CardCostModifier`/`IsAffordable`/`CanAfford`
  hit in `src/frontend/client/src` is prose in a comment (grep: 20 hits, all
  comments). `RuntimeBoundaries.test.ts:460-490` still forbids the terms
  (`EffectiveCardCost`, `CardCostModifier`, `IsAffordable`, `isAffordable`,
  `CanAfford`, `canAfford`, `PowerCost`, `powerCost`, `CostFrom`, `costFrom`) —
  unchanged, not weakened.
* The new formatter translates tokens to units only; it resolves no
  `PercentMaxHp`, derives no damage, and defaults no absent member
  (`ContentEffectFormat.ts:112-150`), pinned by 22 tests.

**New observation (N-08).** That cost guard covers only three runtime files
(`RuntimeBoundaries.test.ts:234-238` — `GameRuntimeState.ts`, `GameRuntime.ts`,
`GameRuntimeEvents.ts`). `BattleScene.ts`, where the cast tiles live, is *not*
scanned. A future cost computation placed in the scene would not trip the guard.

### 3.4 No Signature Skill semantics were introduced — **HOLDS, and this is now the crux of §6**

The widened `effectDefinition` member is definition-generic, no `PetSkill` row is
reachable through it (`API_CONTRACTS.md:862-866`), and the Lobby's card decision
is exactly 3 Basic Cards. But the same widening made the *absence* of any
readable Signature Skill definition more visible, which is why §6 rules it a
data-contract conflict rather than a cosmetic gap.

### 3.5 No client-side catalog or prose reconstruction — **HOLDS**

`ContentEffectFormat.ts:59-76` holds two token→unit translation tables and
nothing else (no per-id table, no hardcoded magnitude); unknown tokens print
verbatim; three tests assert the module's own source for absence of a catalog, of
`Math.*`, and of cost vocabulary. The `LobbyScene` scene holds no token table
(`LobbyScene.ts:125,246`). The one pre-existing client-side content literal is the
Boss list (`LobbyScene.ts:80-86`), which predates this change and is documented as
a closed static list justified by the absence of a Boss endpoint
(`LobbyScene.ts:70-78`).

### 3.6 Existing loadout membership semantics are unchanged — **HOLDS**

`ListCardsAsync` still reads the unlocked set (`CollectionQueryService.cs:183-187,
204-206`); `ListRelicsAsync` still reads owned instances
(`CollectionQueryService.cs:262-334`); membership tests re-asserted
(`ListCards_ShouldReturnOnlyTheCallersUnlockedDefinitions`, …). This audit's live
run independently confirms the only visible membership is `{cards:3, relics:3}`.

---

## 4. TASK-212B Readiness Assessment

### Verdict

```text
DEFER — blocked by (a) TASK-213's acquisition decision, and (b) an
        unresolved intra-document conflict in SIGNALR_PROTOCOL.md
        (§3.2.20 item 2 vs §4 item 15 / §4.3 item 15).
        Outranked in the next slot by TASK-213 and TASK-218.
```

### 4.1 `CardCostModifiers[]` — contract and state: correct, and permanently empty

| Question | Answer | Evidence |
|---|---|---|
| Schema | exactly two members: `SourceIdentity`, `CostReductionPercentage` | `CardCostModifier.cs:97-115`; `GAME_STATE.md` §2.3.5 |
| `PetState` member | `CardCostModifier[] CardCostModifiers { get; init; } = []` | `PetState.cs:637`, init `:940` |
| Sole production writer | the Relic stage's `CardCost` arm | `RelicResolver.cs:639-652`; reached from `BattleStateService.cs:2600-2658` (returns unchanged when `EquippedRelics` is empty, `:2612-2615`) |
| Sole reader | the cast path | `CardCastExecutor.cs:61` |
| Delivered? | **No**, and no event carries it | `SIGNALR_PROTOCOL.md` §4 item 15 `:1503-1537`; §4.3 item 2 `:1750-1767`; §4.3 item 15 `:1987-1992`; `GAME_STATE.md` §2.3.5 item 10; ADR-018 decision 12j |
| Non-empty in a real battle? | **Never** | only `relic-emergency-core` has a `CardCost` effect (`20261003074309_…:210-224`); it is not granted (`PlayerStarterGrantFactory.cs:81-84,90-95`); relics are ownership-checked (`RelicLoadoutService.cs:137-146`) |

`UNVERIFIED`: a hand-inserted database row is outside every code path and was not
excluded by inspecting a live database.

### 4.2 `EffectiveCardCost` — rules confirmed

```text
TotalReduction       = min( sum(CostReductionPercentage), 100 )      CARD_RULES.md §3.6 item 3
RawEffectiveCardCost = CardDefinition.PowerCost × (100 − TotalReduction) / 100
EffectiveCardCost    = truncate(RawEffectiveCardCost)  (toward zero) CARD_RULES.md §3.6 item 4
```

There is no minimum cost; `0` is a real cost (§3.6 item 5); the value is
calculated **once** before validation, deduction and reporting "so a cast cannot
be validated against one cost and charged another" (§3.6 item 7). Implementation:
`EffectiveCardCost.cs:59` (`MaximumTotalReduction = 100`), `:87` (`Compose`),
`:96-109` (sum → cap → truncate).

**This is the reason `powerCost` alone would be wrong.** `CARD_RULES.md:207-210`
fixes that "Card Cost" in §3 items 2 and 4 **is** the Effective Card Cost. So
exposing the authored `PowerCost` on §5.3 and letting the client compare it with
`petState.power` would produce an affordability judgment that is wrong whenever a
modifier is active — and the client is forbidden to correct it (§4.3).

### 4.3 Current Power state delivery — confirmed, with an explicit prohibition

`power` **is** delivered (`SIGNALR_PROTOCOL.md` §4.3 item 15 `:1939-2007`;
`BattleHub.cs:243`; `BattleScene.ts:1076-1077`; live E2E
`phase5b.hudPetPowerIsVisible — "0 / 100"`). The prohibitions are explicit and
threefold:

```text
SIGNALR_PROTOCOL.md:1987-1992  §4.3 item 15
  "power also does not authorize a client-side cost, affordability, or
   cast-legality computation: the effective cost is composed server-side
   (CARD_RULES.md §3.6), CardCostModifiers[] remains undelivered (§4 item 15),
   and the server remains the only validator of a cast."
SIGNALR_PROTOCOL.md:1524-1527  §4 item 15
  "it must never compute, predict, or reconstruct a Card-cost modifier from
   events, from a PowerChanged delta, or from a Card's definition."
SIGNALR_PROTOCOL.md:1521-1522  §4 item 15
  "CardCostModifiers[] stays undelivered, and power authorizes no client-side
   cost, affordability, or legality computation."
```

### 4.4 The current cast path already has authoritative rejection semantics — **yes, and they are already displayed**

`CardCastRejectionReason.cs:6-43` → `CardCastRejectionCodes.ToContractCode:56-67`
yields four documented wire codes; `CardCastExecutor.cs:50-85` rejects before any
write; `BattleHub.cs:1118-1145` returns them through the §5 acknowledgement
(`SIGNALR_PROTOCOL.md:2149-2182`); `BattleScene.ts:1658-1661` renders
`"<action>: rejected (<code>)."`. This audit's live run observed both:
`phase6.castFeedbackReceived — CardCast card-heal: rejected
(INSUFFICIENT_POWER).` and `phase6b.secondCastAcknowledged — CardCast
card-power-charge: accepted.`

**Finding (N-06, reclassified from A-13(iii)).** The affordability *decision* is
therefore already answerable by the server and shown to the player — in a raw
machine code. Mapping those four codes to player-readable text is an
implementation-only presentation change requiring **no** contract decision, and it
is the correct near-term answer to the part of A-04 that is reachable today.
TASK-212B must not absorb it (a wire decision and a copy decision are different
owners) and it must not be used to justify a cost member.

### 4.5 The motivating content is not provisioned-and-reachable — confirmed

`relic-emergency-core` is provisioned (`20260929152651:212`, restructured at
`20261003074309:210-224`) and **deliberately not selected** for the starter set
(`PlayerStarterGrantFactory.cs:81-84`; `DATABASE.md:1313-1315`). No unlock,
purchase, drop, claim, reward or equip endpoint exists among the nine documented
routes (`API_CONTRACTS.md:146-162`), and `IRelicRepository.AddAsync` has **zero
production callers** (§7.3). So implementing cost now would produce **a contract
with no reachable gameplay** — precisely the outcome the task statement asked to
test for.

### 4.6 Would implementing cost now produce meaningful player-facing behaviour?

```text
Meaningful behaviour                 NO  — the modified cost can never differ from
                                           the base cost in a real battle today
                                           (CardCostModifiers[] is always empty).
Base-cost display (if §5.3 gained    MARGINAL and MISLEADING — it is not the
  powerCost)                               price (CARD_RULES.md:207-210) and the
                                           client may not correct it.
Affordability/disabled-tile state    FORBIDDEN — SIGNALR_PROTOCOL.md §4.3 item 15.
Rejection legibility                 YES, and it needs no contract change (§4.4).
```

---

## 5. Pet Passive Assessment

### Verdict

```text
D — intentionally deferred by an explicit Product Owner decision,
    for the EFFECT layer.
    The MECHANISM (charge / threshold / reset / overflow) and the
    PRESENTATION (id, gauge, counter, reset suffix, both events) are
    fully implemented; all five EFFECTS are absent.
```

### 5.1 The decision that produced the deferral — quoted

```text
TASK-191-balance-pass.md:632
  | Q-11 | Approve pet-passive magnitudes for Xích Lang and Sơn Hùng
  |      | (not authored anywhere)?
  |      | ✅ APPROVED — REMAIN UNIMPLEMENTED FOR MVP.
TASK-200-mvp-balance-decision-implementation.md:57
  | Q-11 | Pet Passives | REMAIN UNIMPLEMENTED FOR MVP. Xích Lang and
  |      | Sơn Hùng passives remain unimplemented per MVP scope.
TASK-194:409 / TASK-196:508-512
  the technical finding the decision rested on: pet passive EFFECTS are not
  applied in BattleStateService (only charged/triggered); magnitudes are
  unauthored in docs/.
```

### 5.2 The mechanism is implemented; the effect is not

```text
Implementiert   PassiveTracker.Charge (PassiveTracker.cs:161) — progress,
                threshold evaluation once per Cascade, default/partial/no reset
                (PASSIVE_RULES.md §2/§4/§5)
                consumer 1  BattleStateService.cs:1204-1208 (Pet)
                consumer 2  BattleStateService.cs:1587     (Boss)
                write-back  BattleStateService.cs:1217-1220
                events      BattleStateService.cs:1239-1240
Absent          any effect. :1240 is the sole consumption of charged.Triggers;
                grep PassiveEffect|ApplyPassive|PetPassiveResolver over
                src/backend -> no matches; PetState.PassiveId is branched on
                nowhere in the Pet path.
Contrast        the BOSS branch DOES apply effects:
                BattleStateService.cs:1608-1637
                  "boss-hoa-long-rage" -> TurnBased BuffDebuff +20 ATK / 3 turns
                  "boss-moc-yeu-regen" -> HP heal 5% MaxHP
                and :1705-1775 ("son-thach-ve-enrage", "kim-loi-vuong-combo").
                The Pet branch has no analogous statement.
```

### 5.3 Which of the five effects are implemented vs absent

```text
Pet        PassiveId            Expected effect (PASSIVE_RULES.md:216-224)   Status
---------  -------------------  -------------------------------------------  --------
Xích Lang  passive-xich-lang    Empower next attack + apply Burn             ABSENT
Huyền Quy  passive-huyen-quy    Gain Shield = 15% Max HP                     ABSENT
Bạch Hổ    passive-bach-ho      Next attack gains increased Crit chance      ABSENT
Thanh Xà   passive-thanh-xa     Restore 8% HP                                ABSENT
Sơn Hùng   passive-son-hung     Gain temporary Defense                       ABSENT
```

All five ids are provisioned (`…20260929152651:161,169,177`;
`…20261004055006:190,200`). **No magnitude column exists in any migration**
(`PetDefinition` carries `PassiveId` + `PassiveThreshold` only), no config
constant exists (grep `passiveMagnitude|PassiveDamage|PassiveHeal|PassiveShield`
→ 0 matches), and `PASSIVE_RULES.md:226-229` states the magnitudes "are balance
values and live in config, not in this document". The single numeric passive
value anywhere is `COMBAT_RULES.md:526`'s **worked example**
(`Bạch Hổ's Passive = +10 percentage points`), which `CARD_RULES.md:429` calls
"Bạch Hổ's Pet Passive *configuration* value" — an illustration, not an authored
magnitude. Bạch Hổ's anticipated carrier
(`PetState.NextAttackCritModifiers[]`, ADR-017) is populated only by
`CardCastExecutor.cs:218` and `RelicResolver.cs:632` — never by a passive trigger.

### 5.4 Presentation is implemented, and shows a raw identity

Live, this audit's run:

```text
PASS phase5b.passivePresentationIsReadable
     {"rendered":{"id":"Passive: passive-xich-lang",
                  "progress":"0 / 5 Matches (reset: Default)"},
      "delivered":{"passiveId":"passive-xich-lang","current":0,"threshold":5,
                   "reset":"Default",...}}
```

`BattleScene.ts:1081` renders `Passive: ${state.petState.passiveId}` because
"the client holds no Passive definition to name it with" (`:719-724`). **No
endpoint delivers a passive name**: `API_CONTRACTS.md` has zero matches for
"passive", and `PetCollectionItem.cs:49-55` projects exactly six members. So a
readable passive identity is a second, smaller read-source gap inside the same
task.

### 5.5 The tests pin the payload shape, not the effect's absence

`PassiveEventSourceTests.cs:201-216` and `PassiveTrackerTests.cs:814-831` assert
the *event member set* (`[PassiveId, Progress, Source, SourceId, Threshold]`) and
that no effect-summary member exists. **No test asserts that a Pet passive trigger
leaves `HP` / `ActiveStatusEffects` / `NextAttackCritModifiers` / `ATK`
unchanged** (N-07). The gameplay-effect absence is established by code reading and
by task prose (TASK-194 D-2, TASK-196 Q-11), not by an asserting test.

### 5.6 Assessment

| Layer | Status |
|---|---|
| charge / threshold / reset / overflow | **implemented** |
| `PassiveCharged` / `PassiveTriggered` on the wire | **implemented** (`SIGNALR_PROTOCOL.md` §3.2.16/§3.2.17; deliberate absence of an effect summary, §3.2.25 items 3–5) |
| presentation (identity, counter, gauge, reset suffix) | **implemented** (raw id only) |
| effect summary in the contract | **intentionally omitted** by decision |
| application of any Pet passive effect | **absent, by decision** |

`MVP_SCOPE.md:51-52` still lists "Pet Element, Passive, Signature Skill" as **IN**,
and `GDD.md:192-194,303-306` still requires the player to understand "what their
Pet Passive does and when it triggers". The deferral has **no counterpart
amendment** narrowing `MVP_SCOPE.md` §1 — the conflict TASK-207 G8 / TASK-212 A-06
recorded is still open. That is the decision this audit re-ranks (TASK-215, §10).

---

## 6. Signature Skill Assessment

### Verdict

```text
CONTRACT DRIFT — a data-contract conflict between two authoritative
technical documents — with an implementation-level presentation
consequence and a test-masking coverage defect.

NOT an implementation bug. NOT purely decision-gated: the conflict is
itself the blocker, and it is an AGENTS.md §4/§20 stop condition.
```

### 6.1 The conflict, stated exactly

`SIGNALR_PROTOCOL.md` §4.3 item 13 (`:1861-1865`) instructs the client how to
identify the Signature Skill entry:

> **Signature Skill identification:** Exactly one entry in `equippedCards`
> is the active Pet's Signature Skill (`Category == PetSkill`,
> `CARD_RULES.md` §4, **`API_CONTRACTS.md` §5.3**).

`API_CONTRACTS.md` §5.3 (`:862-866`) states that source can never supply it:

> **Membership is unchanged.** Presence in this array is still the unlocked state:
> … a `PetSkill` `CardDefinition` is still never an unlock row
> (`CARD_RULES.md` §1 item 4, ADR-012 item 9), and the member is
> definition-generic — **no `PetSkill` row reaches it because none is ever an
> unlock row.**

The implementation honours §5.3: no migration inserts a `PlayerUnlockedCard` row;
the starter grant excludes PetSkill by contract
(`PlayerStarterGrantFactory.cs:64-67,69-74`); a submitted PetSkill entry is
rejected `NotBasic` (`CardLoadoutService.cs:161-173`). And **no third source
exists**: `POST /api/battle/start` returns `battleId` / `signalrHub` /
`initialState` only and the client deliberately does not read `initialState`
(`GameRuntime.ts:562-567`); `GET /api/pets` is the exhaustive six-member list
`petId, identity, element, tier, star, level` (`API_CONTRACTS.md:719-731,752-753`)
and does not expose `SignatureSkillCardId`; there is no positional rule
(`CardLoadoutService.cs:222`: "Order carries no gameplay significance").

So `SIGNALR_PROTOCOL.md` §4.3 item 13 **requires the client to read a datum from a
source its own sibling contract forbids from carrying it.** `BattleScene.ts:1230-1231`
implements §4.3 item 13 literally — the code is not contradicting its instruction;
the two documents contradict each other.

A second, smaller contradiction sits in the same item: `:1859-1860` states "the
client maintains no local registry and performs no card validation", while
`BattleScene.ts:382,1156-1173` maintains exactly such a registry
(`cardDefinitions`) and reads `category` from it (N-04).

### 6.2 The production behaviour, confirmed live by this audit

Source: entry 4 of `petState.equippedCards` is the derived Signature Skill
(`SIGNALR_PROTOCOL.md` §4.3 item 13; `CardLoadoutService.cs:224-232`).
`BattleScene.loadCardDefinitions` fills `cardDefinitions` **only** from
`runtime.getCards()` → `GET /api/cards` → the unlock set. Therefore
`def` is `undefined` for `card-inferno`, `isPetSkill === false`, and:

```text
BattleScene.ts:1231-1233   label   `Card: ${def?.name ?? cardId}`  -> "Card: card-inferno"
BattleScene.ts:1253-1258   click   submitCardCast('card-inferno')  -> CardCast(card-inferno)
```

This audit's own browser run observed it **in both runs**:

```text
PASS  phase6d.signatureSkillControlRemainsUsable
      CardCast card-inferno: rejected (INSUFFICIENT_POWER).
```

So the 4th tile's request path and its rendered action name are confirmed
against the real stack, not inferred from source alone.

### 6.3 Correction to TASK-212 A-01 and A-02

**A-02 is REFUTED.** TASK-212 classified the executor's acceptance of a `PetSkill`
card as "contradicting its own documented contract". It does not:

* `CARD_RULES.md` §6 (`:487-491`): "`CardCast` — emitted for every successful
  **Basic Card or Pet Skill Card** cast"; "`PetSkillCast` — emitted specifically
  when the cast Card is the active Pet's Signature Skill (**in addition to**
  `CardCast`)".
* `SIGNALR_PROTOCOL.md` §3.2.20 item 4 (`:1103-1106`): "**Emitted for every
  successful cast** … §6 fixes that `CardCast` fires 'for every successful Basic
  Card or Pet Skill Card cast'."
* `SIGNALR_PROTOCOL.md` §3.2.22 item 1 (`:1156-1160`): "`CardCast` is emitted
  first, then `PetSkillCast`."
* `CARD_RULES.md` §3 item 2 validations name loadout membership, Power and
  Card-specific preconditions — **no category restriction** on the casting path.

The executor matches the domain rule exactly (`CardCastExecutor.cs:55-59` cites
"`CARD_RULES.md` §1, §3, §4"; `:120-123` emits `PetSkillCast` in addition), and
`tests/backend/GameServer.Domain.Tests/Cards/CardCastExecutorTests.cs:309-339`
pins the event order `[CardCast, PowerChanged, PetSkillCast, DamageCalculated,
DamageDealt, DamageTaken]`. **The stale side is prose, not behaviour**:
`BattleHub.cs:1107-1110` ("Executes one requested **Basic** Card cast") and
`CardCastRejectionReason.cs:26-30` ("not a Basic Card"). Reclassified:
`IMPLEMENTATION-ONLY, P2` → `DOCUMENTATION-ONLY, P3` (N-01).

**A-01 is SPLIT.** The "wrong action" half is refuted by the same rule set:
`CardCast(<signature cardId>)` and `PetSkillCast(battleId)` reach the **same**
`CardCastExecutor.Execute` (`BattleStateService.cs:989` vs `:1070`), deduct the
same `EffectiveCardCost`, and emit the same event sequence; the only differences
are where the definition is sourced and which early check trips on malformed
input (`CARD_NOT_IN_LOADOUT` vs `INVALID_CARD`). The live rejection is a
*cost* rejection — `PetSkillCast` would have returned `INSUFFICIENT_POWER` at the
same Power. The presentation half is real, and its root cause is upgraded from
"missing read source" to **the §6.1 data-contract conflict**.

### 6.4 The tests give the client a definition production cannot obtain — confirmed

`SceneLifecycle.test.ts:308-313` injects the impossible row:

```ts
getCards: vi.fn(async () => [
  { cardId: 'card-heal',        name: 'Heal',        category: 'Basic'    as const },
  { cardId: 'card-shield',      name: 'Shield',      category: 'Basic'    as const },
  { cardId: 'card-power-charge',name: 'Power Charge',category: 'Basic'    as const },
  { cardId: 'card-inferno',     name: 'Inferno',     category: 'PetSkill' as const },
]),
```

The mask is exact and load-bearing: the *entire* label/dispatch branch is
`def?.category === 'PetSkill'` (`BattleScene.ts:1231`), and this stub is the only
thing in the suite that ever supplies that category. The assertions that follow
therefore certify behaviour production cannot reach:

```text
:3123-3133  renders cast controls ...           expect(rendered).toContain('Skill: Inferno')
:3135-3147  derives the signature skill ...     clickables.find(c => c.text.includes('Skill: Inferno'))
:3162-3173  submits PetSkillCast ...            expect(requestedActions).toEqual([{kind:'PetSkillCast'}])
:3190-3198  displays rejection reason ...       expect(...).toContain('PetSkillCast: rejected (INSUFFICIENT_POWER).')
:2502-2504  names a cast callout ...            expect(callout().text).toBe('PET SKILL: Inferno')
```

Production produces `Card: card-inferno`, `{kind:'CardCast', cardId:'card-inferno'}`,
`CardCast card-inferno: rejected (INSUFFICIENT_POWER).` and `PET SKILL: card-inferno`.
`grep 'Card: card-' tests/` → **no matches**: the fallback path is untested (N-02).

**The E2E does not catch it either (N-03).** `standalone-web-smoke.mjs:2441-2451`
locates the tile *positionally* (`equippedCards.length - 1`) and `:2471-2476`
accepts any acknowledgement text:

```js
record('phase6d.signatureSkillControlRemainsUsable',
  typeof skillFeedback === 'string' &&
    (skillFeedback.includes('accepted') || skillFeedback.includes('rejected')),
  skillFeedback);
```

The script never mentions `CardCast` or `PetSkillCast`. The check passes
identically for the degraded path and the intended one — and indeed it passed in
this audit's run *with* the degraded path's own message.

### 6.5 Classification and recommendation

| Aspect | Classification |
|---|---|
| `SIGNALR_PROTOCOL.md` §4.3 item 13 ↔ `API_CONTRACTS.md` §5.3 | **contract drift / data-contract conflict** (`AGENTS.md` §4, §20) |
| 4th tile label `Card: card-inferno` | implementation consequence of the conflict; presentation defect |
| `CardCast` dispatch on the signature tile | **not a defect** (contract-conformant; refutes TASK-212 A-01/A-02) |
| `BattleHub.cs:1107-1110`, `CardCastRejectionReason.cs:26-30` | stale prose (P3, documentation-only) |
| `SIGNALR_PROTOCOL.md:1859-1860` "no local registry" | stale prose vs the implemented registry (P3) |
| `SceneLifecycle.test.ts:308-313` + method-agnostic E2E check | coverage defect — the suite certifies a false picture of the primary battle surface |

**Recommendation.** This **stays TASK-219**, but it must be **split** and
**re-scoped**, and it must **not** be promoted above the reason it was promoted
in TASK-212 (a "functional wrong-RPC defect"). Specifically:

```text
TASK-219A  Resolve the §4.3 item 13 identification source (DECISION + doc
           amendment + implementation). Owner: SIGNALR_PROTOCOL.md §4.3 item 13
           with one of API_CONTRACTS.md §5.1 (a Pet-read member), §3 (the
           battle-start response), or a new read. §5.3's membership must NOT be
           widened (that would make a PetSkill card an unlock row and contradict
           CARD_RULES.md §1 item 4 / ADR-012 item 9).                      M
TASK-219B  Make the client harness model production (no PetSkill row from
           getCards) and assert the real fallback, so the suite stops certifying
           an unreachable picture; tighten phase6d to assert the action it
           observed.                                                       S
TASK-217B  Correct the three stale prose sites.                             S
```

Priority: **P2** for the player-visible consequence (a raw id on one tile), with a
hard **P1 conflict marker** because two authoritative documents disagree. Effort
M. Dependency: soft on TASK-213 (how much Signature Skill content matters depends
on how many Pets are reachable).

---

## 7. Content Reachability Matrix

Levels are distinguished deliberately: *exists in a definition row* is **not**
*reachable gameplay*.

```text
EX  content exists (a provisioned definition row)
RD  content is readable by a client (some response delivers it)
SEL content is selectable by the player (the client presents it as a choice)
USE content is mechanically usable (the server accepts and resolves it in battle)
ACQ content can be acquired by normal player progression after account creation
```

| Content class | Count provisioned | EX | RD | SEL | USE | ACQ |
|---|---|---|---|---|---|---|
| **Pets** | 5 | ✅ | ⚠️ **owned only** — only `pet-xich-lang` can reach a client | ✅ (singleton) | ✅ | ❌ |
| **Basic Cards** | 3 | ✅ | ✅ | ✅ | ✅ | ❌ (all 3 granted at creation) |
| **Pet Skill / Signature Cards** | 5 | ✅ | ❌ **never** | ❌ | ✅ (derived server-side; castable) | N/A — never an unlock row by design |
| **Relics** | 10 | ✅ | ⚠️ **owned only** — only the 3 starter definitions | ✅ (forced: all 3, any order) | ✅ | ❌ |
| **Bosses** | 5 | ✅ | ❌ **no endpoint**; identity via battle-start only | ✅ (5 free choices) | ✅ | N/A — no ownership relationship |

### 7.1 Provisioned content (verified by migration `InsertData`)

```text
Pets    5  pet-xich-lang, pet-bach-ho, pet-huyen-quy
           (20260929152651:161,169,177) + pet-thanh-xa, pet-son-hung
           (20261004055006:190,200)
Cards   8  Basic     card-heal, card-shield, card-power-charge (20260929152651:104,111,119)
           PetSkill  card-inferno, card-tidal-barrier, card-iron-fang (…:127,134,142)
                     card-venomous-bloom, card-earthshaker (20261004055006:140,160)
Relics 10  relic-berserker-core, -mana-crystal, -assassin-eye, -emergency-core
           (20260929152651:190,197,204,212) + relic-burning-curse, -combo-fang,
           -arcane-battery, -execution-mark, -cascade-core, -battle-instinct
           (20261004153916:185,205,233,254,276,308)
Bosses  5  boss-def-hoa-long, -thuy-ma, -moc-yeu (20260926151112:71,82,91)
           boss-def-son-thach-ve, -kim-loi-vuong (20261004094100:118,131)
Total  28  definitions provisioned;  7 ownable rows ever created
```

### 7.2 The single acquisition event is Player creation

```text
Register / Login
  -> AuthController.cs:99 (register), :143 (login fallback)
  -> PlayerRepository.GetOrCreateForAccountAsync (:26-96)
  -> new-Player branch only (:33-39 returns early for an existing Player)
  -> StageStarterOwnership (:125-169) — Pets.Add :139, PlayerUnlockedCards.Add :150,
     Relics.Add :164
  -> one SaveChangesAsync = 1 Pet + 3 Cards + 3 Relics (7 ownership rows)
```

`DATABASE.md:1291-1318` fixes the composition; `:1320-1327` classifies it as
"MVP bootstrap, not acquisition gameplay" that "do[es] not define or constrain
future gameplay acquisition systems"; `:1336-1338` forbids conditional top-ups.

### 7.3 Ownership cannot grow — verified by exhaustive caller search

| Repository method | Declaration / implementation | Production callers |
|---|---|---|
| `ICardRepository.AddUnlockAsync` | `ICardRepository.cs:48`; `CardRepository.cs:38-44` | **0** |
| `IPetRepository.AddAsync` | `IPetRepository.cs:42`; `PetRepository.cs:32-36` | **0** |
| `IRelicRepository.AddAsync` | `IRelicRepository.cs:33`; `RelicRepository.cs:27-31` | **0** |
| `ICardRepository.AddDefinitionAsync` / `IRelicRepository.AddDefinitionAsync` | `ICardRepository.cs:38`; `IRelicRepository.cs:40` | **0** (test doubles only) |
| `IBattleResultRepository.AddAsync` | `IBattleResultRepository.cs:82` | `BattleResultService.cs:492` — writes a `BattleResult`, creates **no ownership** |

No unlock / purchase / drop / claim / reward / equip endpoint exists in the
nine-route surface (`API_CONTRACTS.md:146-162`). The only `RelicInstanceId` mint
in production is `PlayerStarterGrantFactory.cs:234`.

### 7.4 The loadout is a forced permutation — confirmed live

| Slot | Requirement | Owned | Verdict |
|---|---|---|---|
| Pet | exactly 1, owned | 1 (`pet-xich-lang`) | **forced** |
| Cards | exactly 3, owned, `Category = Basic`, copy limit 1 each | 3 Basics | **forced** — 6 orderings only |
| Relics | 3–5, owned, pairwise-distinct instances | 3 | **forced** — 6 orderings only |
| Boss | exactly 1 valid MVP identity | 5 global | **free** — the one real decision |

Live confirmation: `phase4.starterGrantLoaded — {"pets":1,"cards":3,"relics":3}`
and `phase8.preservedLoadoutRestored` restoring exactly that permutation.

### 7.5 Collection vs active Battle loadout

They are **independent readers of the same three endpoints**. The
CollectionViewer holds only `selectedItemId` for its detail panel, emits nothing,
and has no import of `PreservedLoadout`; the Lobby holds the four real selection
state members and is the only surface that posts `POST /api/battle/start`. The
collection therefore does **not** drive the loadout, and (because the candidate
set is fixed) the collection can never change what the loadout offers.

### 7.6 The document conflict this exposes

`MVP_SCOPE.md:49-76` lists 5 Pets / 3 Basic Cards + 5 Pet Skill Cards / ~10 Relics
/ 5 Bosses as **IN**; `MVP_SCOPE.md:148` makes anything absent from §1 and §2
**FUTURE** by default; no acquisition mechanism is documented anywhere; and
`DATABASE.md` §2 owns what an account actually owns. Reachable content is
1 / 3 / 0 / 3 / 5. This is the conflict **TASK-213 owns** (§11).

---

## 8. Newly Discovered Gaps

Each finding gives behaviour, evidence, contract reference, player impact,
priority, effort, contract impact and recommended action.

### N-01 — `CardCast` document prose describes a narrower rule than the domain rule and the executor

```text
Behaviour        BattleHub.cs:1107-1110 documents CardCast as "Executes one
                 requested Basic Card cast"; CardCastRejectionReason.cs:26-30
                 says InvalidCard means "not a Basic Card". The domain rule and
                 the executor admit Basic *or* PetSkill.
Evidence         CARD_RULES.md §6 (:487-491); SIGNALR_PROTOCOL.md §3.2.20 item 4
                 (:1103-1106), §3.2.22 item 1 (:1156-1160), §3.2.21 item 5
                 (:1145-1148); CardCastExecutor.cs:55-59,120-123;
                 CardCastExecutorTests.cs:309-339 (event order pinned)
Contract ref     CARD_RULES.md §3, §6 own the rule; the two comments are stale
Player impact    None directly; it is the stale text that made TASK-212 A-02
                 misclassify conformant behaviour as a defect
Priority         P3            Effort  S
Contract impact  DOCUMENTATION-ONLY (code comments)
Action           Correct both comments in the same change as TASK-217B
```

### N-02 — The client test harness fabricates the Signature Skill definition production cannot obtain

```text
Behaviour        SceneLifecycle.test.ts:308-313 stubs GET /api/cards with a
                 category:'PetSkill' card-inferno row. The entire label/dispatch
                 branch is `def?.category === 'PetSkill'` (BattleScene.ts:1231),
                 so the stub is the only thing that ever exercises the intended
                 path. No test covers the production fallback.
Evidence         SceneLifecycle.test.ts:308-313 (stub), :3123-3133, :3135-3147,
                 :3162-3173, :3190-3198, :2479-2513; BattleScene.ts:1230-1233,
                 1253-1258, 1156-1173; grep 'Card: card-' tests/ -> no matches
Contract ref     API_CONTRACTS.md §5.3 (:862-866) — no PetSkill row is ever an
                 unlock row, so production cannot supply the stub
Player impact    None directly, but the suite now certifies behaviour the player
                 never sees, so a regression on the real path would be invisible
Priority         P1 (verification integrity on the primary battle surface)   Effort S
Contract impact  NONE
Action           TASK-219B: make the harness model production and assert the
                 actual fallback (and, separately, the *intended* path once §6.1
                 is decided). Do not delete the assertions — invert their premise
```

### N-03 — The E2E Signature Skill check cannot detect the degraded path

```text
Behaviour        phase6d locates the tile positionally and accepts any
                 acknowledgement text ('accepted' | 'rejected'); it never
                 asserts which action was requested.
Evidence         standalone-web-smoke.mjs:2441-2451 (positional lookup),
                 :2471-2476 (the method-agnostic assertion); the file contains no
                 occurrence of CardCast/PetSkillCast
Player impact    None directly; it is why a live regression on this control has
                 never been caught in a smoke run
Priority         P2            Effort  S
Contract impact  NONE
Action           TASK-219B: assert the observed action/reason against the
                 delivered category source
```

### N-04 — `SIGNALR_PROTOCOL.md` §4.3 item 13 claims the client keeps no registry while the client keeps one

```text
Behaviour        :1859-1860 — "the client maintains no local registry and
                 performs no card validation"; BattleScene maintains
                 `cardDefinitions` (declared :382, filled :1156-1173, read
                 :1230, :2016) and reads `category` from it.
Evidence         SIGNALR_PROTOCOL.md:1855-1860 vs BattleScene.ts:382,1156-1173,1230
Contract ref     SIGNALR_PROTOCOL.md §4.3 item 13 owns the sentence
Player impact    None directly
Priority         P3            Effort  S
Contract impact  DOCUMENTATION-ONLY (a protocol-owner wording correction)
Action           Fold into TASK-219A's amendment (the same item is being rewritten)
```

### N-05 — `SIGNALR_PROTOCOL.md` conflicts with itself about whether a client may read a Card definition for cost

```text
Behaviour        §3.2.20 item 2 (:1098-1100): "A client that must show the spent
                 Cost reads the Card's definition". §4 item 15 (:1524-1527):
                 the client "must never compute, predict, or reconstruct a
                 Card-cost modifier from events, from a PowerChanged delta, or
                 from a Card's definition". §4.3 item 15 (:1987-1992): "power
                 also does not authorize a client-side cost, affordability, or
                 cast-legality computation".
Evidence         the three quoted sites; CARD_RULES.md §3.6 items 2/4
                 (:250-255,274-292) — the spent cost is EffectiveCardCost and is
                 "not stored state"; API_CONTRACTS.md:867 (powerCost not exposed);
                 SIGNALR_PROTOCOL.md:1091-1100 (CardCast carries no cost member);
                 the one delivered value equal to the spend is the cost-side
                 PowerChanged delta, itself forbidden as a source
Contract ref     SIGNALR_PROTOCOL.md — intra-document, so AGENTS.md §2 precedence
                 cannot decide it; needs the protocol owner (AGENTS.md §4/§20)
Player impact    Indirect: it is the reason TASK-212B cannot be implemented and
                 the reason the client currently shows a raw rejection code
Priority         P1 (contract-integrity blocker for TASK-212B)   Effort  S (decision)
Contract impact  DECISION-REQUIRED (ratify §4 item 15 / §4.3 item 15 as governing
                 and correct §3.2.20 item 2's sentence)
Action           Take the decision in TASK-212B's decision round; it blocks
                 TASK-212B and nothing else
```

### N-06 — Affordability feedback exists but is shown as a raw machine code

```text
Behaviour        The server returns four documented rejection codes
                 (CARD_NOT_IN_LOADOUT, INSUFFICIENT_POWER, INVALID_CARD,
                 CARD_CAST_ALREADY_USED_THIS_TURN) and the client prints them
                 verbatim: "CardCast card-heal: rejected (INSUFFICIENT_POWER)."
Evidence         CardCastRejectionReason.cs:6-43,56-67; CardCastExecutor.cs:50-85;
                 BattleHub.cs:1118-1145; SIGNALR_PROTOCOL.md §5 :2149-2182;
                 BattleScene.ts:1658-1661; live E2E phase6.castFeedbackReceived
Contract ref     SIGNALR_PROTOCOL.md §5 owns the codes; no contract change needed
Player impact    The player learns "you cannot afford this" in machine language —
                 the reachable part of GDD.md:218-222 today
Priority         P2            Effort  S
Contract impact  NONE (implementation-only presentation)
Action           Fold into the player-facing-strings task; it is explicitly NOT
                 TASK-212B and must not be used to justify a cost member
```

### N-07 — No test asserts a Pet passive trigger leaves gameplay state unchanged

```text
Behaviour        The absence of the five passive effects rests on code reading
                 and on task prose (TASK-194 D-2, TASK-196 Q-11), not on a test.
                 The existing pins assert the *event member set*, not the effect.
Evidence         PassiveTrackerTests.cs:814-831; PassiveEventSourceTests.cs:201-216;
                 BattleStateService.cs:1204-1240 (events only) vs :1608-1637
Contract ref     PASSIVE_RULES.md §7, §8 vs the TASK-200 Q-11 deferral
Player impact    None directly; the risk is that the deferral silently becomes an
                 implementation default rather than a recorded decision
Priority         P3            Effort  S
Contract impact  NONE
Action           Fold into TASK-215: whichever way the decision goes, pin it
```

### N-08 — The cost boundary is enforced in the runtime but not where cast tiles live

```text
Behaviour        RuntimeBoundaries.test.ts scans only GameRuntimeState.ts,
                 GameRuntime.ts and GameRuntimeEvents.ts for cost terms; a cost
                 computation placed in BattleScene.ts would not trip the guard.
Evidence         RuntimeBoundaries.test.ts:234-238 (the three-file scope),
                 :240-310 (loop), :460-490 (the cost term set)
Contract ref     SIGNALR_PROTOCOL.md §4 item 15 / §4.3 item 15 (the boundary)
Player impact    None directly; it is a guard-coverage asymmetry
Priority         P3            Effort  S
Contract impact  NONE
Action           Extend the scan to the scenes in the same change that next
                 touches the cost boundary (not now — it would fail on existing
                 comments; see §12's note on why this audit did not change tests)
```

### N-09 — Boss content is written in four literals across three layers, and nothing cross-checks them

```text
Behaviour        The same five Bosses are written four times:
                   (1) BossDefinition.BossDefinitionId in the provisioning
                       migrations — "boss-def-hoa-long" …;
                   (2) BossDefinition.Identity in the same migration rows —
                       "boss-hoa-long" …, which is the canonical technical
                       Identity POST /api/battle/start accepts and
                       BossState.BossId reports (BOSS_RULES.md §6.4);
                   (3) the Domain static definitions BossDefinitions.All, which
                       is what battle composition actually uses
                       (BattleStartService.ResolveBoss :505-521) — the
                       provisioned rows serve only the BattleResult FK via
                       IBossDefinitionLookup;
                   (4) the client's documented closed list MVP_BOSSES.
                 Nothing asserts that the four agree.
Evidence         migrations 20260926151112:71,82,91 and 20261004094100:118,131
                 (both rows and the (1)≠(2) contract are documented in the
                 migration header, e.g. "the three-way contract BossDefinitionId
                 ≠ Identity ≠ …"); BossDefinitions.cs:330-345; LobbyScene.ts:80-86
                 (justified at :70-78 by the absence of a Boss endpoint);
                 live E2E phase4.fiveBossesRendered
Contract ref     BOSS_RULES.md §6/§6.4 owns the identities; DATABASE.md §1 note
                 item 2 owns the (1)/(2) split; API_CONTRACTS.md has no Boss read,
                 so the client literal is a documented exception, not an
                 AGENTS.md §7 client-side catalog
Player impact    None today, and the layering is deliberate and documented. The
                 residual risk is drift: a sixth Boss, or an Identity correction,
                 needs four edits and can be made inconsistently with nothing
                 failing
Priority         P3            Effort  S
Contract impact  NONE (a consistency/test concern, not a contract defect)
Action           Report only; a cheap "the four sets agree" test would close it,
                 and it belongs with whichever task next adds or renames a Boss
```

### N-10 — Stale in-code comments the TASK-217 sweep list does not contain

```text
Behaviour        (a) BattleStartService.cs:248-253 states "BOSS_RULES.md §6
                 defines exactly three content-defined MVP Bosses and
                 BossDefinitions holds exactly those" — five exist and are
                 offered.
                 (b) BattleStateService.cs:1582-1584 states "Passive EFFECT
                 application is out of this task's scope for every Boss … a
                 trigger emits its event and applies nothing" — directly
                 contradicted by the Boss effect block at :1608-1637 (and
                 :1705-1775) implemented by TASK-153.
Evidence         the two sites, plus BossDefinitions.cs:338-345 and
                 BattleStateService.cs:1608-1637
Contract ref     BOSS_RULES.md §6 (identities), BOSS_RULES.md §6.2 + TASK-153
Player impact    None directly; both are the "stale claim a future implementer
                 would act on" class TASK-217 exists for
Priority         P3            Effort  S
Contract impact  DOCUMENTATION-ONLY (code comments)
Action           Add both to TASK-217A's list; they are new to it
```

### N-11 — The verified chain is entirely uncommitted, and no task owns a commit step

```text
Behaviour        HEAD is still afe5b14. TASK-208 / 208A / 210 / 211 / 212A /
                 212A-1 have produced 37 modified tracked files and 11 untracked
                 entries, including the whole API_CONTRACTS.md §5.3/§5.4
                 amendment and 10 completed task records. Every prior audit
                 recorded the same condition at a smaller size (TASK-211 §2.5:
                 20 modified / 4 untracked; TASK-212 §0: 26 / 6): the tree grows
                 monotonically and nothing is ever committed.
Evidence         git status --porcelain; git log --oneline -1; TASK-211 §2.5;
                 TASK-212 §0
Contract ref     tasks/TASK_LIFECYCLE.md §3; tasks/README.md
Player impact    None. Risk: `git checkout`/`stash`/`clean` would destroy a
                 contract amendment and five tasks of verified work, and every
                 future task starts from a tree where its own diff is
                 indistinguishable from its predecessors'
Priority         P1 (process/durability; zero player impact, which is why it is
                 not ranked in §10's product order)   Effort  S
Contract impact  NONE
Action           A commit/integration step before the next implementation task.
                 This audit does not commit (it must not touch the tree beyond
                 its own record) — it reports the precondition
```

### N-12 — Traceability: TASK-209 still has no record (carried from A-19(i))

```text
Behaviour        TASK-210 declares TASK-209 DONE and shipped code/tests cite its
                 section numbers, but no TASK-209 record exists.
Evidence         glob tasks/**/*209* -> none; cited in BattleScene.ts, App.tsx,
                 RuntimeBoundaries.test.ts, SceneLifecycle.test.ts,
                 BattleEventPresentation.test.ts, standalone-web-smoke.mjs
Contract ref     tasks/README.md:95; tasks/TASK_LIFECYCLE.md §3
Player impact    None. An audit does not reconstruct another task's record
Priority         P3            Effort  S
Contract impact  NONE
Action           Bookkeeping; unchanged from TASK-212 A-19(i)
```

Also reported, unchanged and consequently **not** restated as new: A-08 (Boss
`StatusEffects[]` undelivered — `SIGNALR_PROTOCOL.md` §4.4 item 2 `:2038-2044`
still excludes it, so a Card's Burn on the Boss remains invisible); A-12
(`CollectionViewerScene` run guard); A-13 (player-facing technical strings,
including the passive raw id and the raw set ids); A-14 (`USERNAME_ALREADY_TAKEN`
vs `USERNAME_EXISTS`); A-15 (no logout, 401 never resets the session); A-16 (no
battle exit); A-18 (Lobby `MAX_LIST_ROWS = 8` silent truncation at
`LobbyScene.ts:107,1274,1298,1329` — latent today, live the moment ownership can
grow).

---

## 9. Reclassified / Closed Previous Gaps

| Prior ID | Prior claim | This audit's verdict | Evidence |
|---|---|---|---|
| **A-03** | Card/Relic effects invisible at the decision point | **CLOSED** — implemented and verified live | §3.1, §3.2; E2E `phase4.cardRowsStateWhatTheyChange`, `phase4.relicRowsStateTriggerConditionAndEffect` |
| **A-02** | `CardCast` accepts `PetSkill` cards, "contradicting its own documented contract" | **REFUTED** — the executor matches the domain rule; the stale side is two code comments. P2 IMPLEMENTATION-ONLY → P3 DOCUMENTATION-ONLY | §6.3; `CARD_RULES.md:487-491`; `SIGNALR_PROTOCOL.md:1103-1106,1156-1160`; `CardCastExecutor.cs:55-59,120-123`; `CardCastExecutorTests.cs:309-339` |
| **A-01** | Signature Skill tile "mislabelled *and* dispatches the wrong action"; P1; "works only because the server's validation is broader than its documentation" | **SPLIT.** The *dispatch* half is **refuted** (both hub methods reach the same executor and emit the same events; the live rejection is a cost rejection). The *label* half is real, and its root cause is **upgraded** to a data-contract conflict between `SIGNALR_PROTOCOL.md` §4.3 item 13 and `API_CONTRACTS.md` §5.3. Priority **P2** (not P1), with a P1 conflict marker | §6.1, §6.2, §6.3 |
| **A-04** | Cost/affordability cannot be shown and the client is forbidden to derive it; P2; defer behind TASK-213 | **CONFIRMED and sharpened.** It now also names an **intra-document** conflict (§3.2.20 item 2 vs §4 item 15/§4.3 item 15) that `AGENTS.md` §2 cannot resolve, and identifies a decision-free slice (rejection legibility) that is *not* TASK-212B | §4; N-05, N-06 |
| **A-05** | Build diversity is not exercisable; 15 of 28 definitions unreachable | **CONFIRMED with a full five-level matrix and runtime proof.** Refined count: 28 provisioned, **7 ownable**; `{"pets":1,"cards":3,"relics":3}` observed live | §7; E2E `phase4.starterGrantLoaded` |
| **A-06** | The Pet Passive effect is inert; TASK-211's audit dropped the finding | **CONFIRMED and made precise.** Verdict **D** for the effect layer; all five effects absent; magnitudes authored nowhere; the four boss passives *are* implemented, which is the structural contrast | §5 |
| **A-07** | `RelicTriggered` reaches the client and is discarded; implementation-only, S | **CONFIRMED open and untouched.** `BattleScene.ts` still has **zero** `Relic` matches; `describeEventCallout` still returns `null` for it and still calls it a "contract gap" (the misclassification TASK-212 corrected) | `BattleScene.ts` (no Relic matches); `BattleEventPresenter.ts:624-628,680-681` |
| **A-08** | Boss status effects undelivered | **CONFIRMED open**; the protocol still excludes them explicitly | `SIGNALR_PROTOCOL.md:2038-2044` |
| **A-10** | TASK-216's named ordering risk is refuted; the real exposure is three-write non-atomicity | **Unchanged, not re-examined** — no file on that path changed since TASK-212 | TASK-212 §A-10 |
| **A-11(a)** | `ARCHITECTURE.md:641-665` names eight non-existent components; §1 trees do not match `src/`; `:593-594` HUD claim stale | **CONFIRMED open**; unchanged | TASK-212 §A-11(a) |
| **A-19(i)** | No TASK-209 record | **CONFIRMED open**; restated as N-12 | glob `tasks/**/*209*` → none |
| **A-19(vii)** | TASK-210 misclassified the Relic gap as a contract gap | **CONFIRMED** — the misclassification is still present in the *code comment* at `BattleEventPresenter.ts:624-628` | same |

**Closure summary.** Of the 20 TASK-212 gaps: 1 closed (A-03), 2 refuted or split
(A-02 refuted; A-01 split), 4 confirmed and sharpened (A-04, A-05, A-06, A-07),
the remainder unchanged and still open. **No gap was silently dropped** — the
TASK-211 audit's loss of A-06's predecessor (TASK-207 G8) is not repeated here.

---

## 10. Priority-Ranked Next Tasks

Scoring: **Priority** P0 blocks the core loop · P1 major player-facing or
documented-intent violation, or a contract conflict · P2 important
progression/polish · P3 nice-to-have. **Effort** S ≤ 1 session · M 2–4 · L 5+.
The rank is the recommended order, not a restatement of the old numbering.

| # | Task | Title | Priority | Player impact | Contract impact | Effort | Dependency | Decision required |
|---|---|---|---|---|---|---|---|---|
| 1 | **TASK-213** | Content reachability: 5 Pets / 5 Pet Skill Cards / ~10 Relics vs starter-only ownership | **P1** | Structural — `GDD.md:61-62` pillar 2; gates the *value* of TASK-212A-1; gates TASK-212B | DECISION-REQUIRED (`MVP_SCOPE.md` §1 vs `DATABASE.md` §2; any mechanism is a Rule Change and may be FUTURE) | S | none | **YES** |
| 2 | **TASK-218** | Name the Pet's Relic triggers in battle from the read the client already has | P2 | Every battle: 2–3 Relic firings per battle currently produce no feedback; the ATK/Crit/Power modifiers they apply have no attribution | **NONE** (implementation-only; `GET /api/relics` already maps instance→name, `GameRuntimePort.getRelics()` exists) | S | none; runs in parallel with #1 | no |
| 3 | **TASK-219** (split) | Signature Skill identification: resolve `SIGNALR_PROTOCOL.md` §4.3 item 13 vs `API_CONTRACTS.md` §5.3; then fix label + stop masking it | P1 as a **conflict**, P2 as player-visible | One tile on the primary battle surface shows a raw developer id, every battle | DECISION-REQUIRED (`API_CONTRACTS.md` §5.1 or §3 — **not** §5.3's membership) | M | soft on #1 | **YES** |
| 4 | **TASK-215** (expanded) | Documented-but-inert Pet progression: the five Passive effects and Tier/Star | P1 as a **conflict**, P2 as payoff | A documented MVP system's payoff never happens; the gauge fills for nothing; `GDD.md:192-194,303-306` unmet | DECISION-REQUIRED (`MVP_SCOPE.md:51-53` + `PASSIVE_RULES.md` §8 vs TASK-200 Q-11; magnitudes unauthored) | S decision + M impl | none | **YES** |
| 5 | **TASK-217A** | Architecture / component / directory reconciliation (+ N-10's two stale comments) | P1 documentation-blocker | Indirect — `AGENTS.md` §6 routes implementers to eight types that do not exist | DOCUMENTATION-ONLY | M | none | partly (A-20 Discord residue) |
| 6 | **N-11 commit** | Commit / integrate the verified TASK-208 → TASK-212A-1 chain | P1 process (zero player impact) | None; a durability precondition for everything below | NONE | S | none | no |
| 7 | **A-08 task** | Boss status-effect projection (Burn/rage/shield) | P2 | A Card's Burn on the Boss is invisible; the player's own damage-over-time has no readout | DECISION-REQUIRED (`SIGNALR_PROTOCOL.md` §4.4 — the TASK-208 D-208-03 shape) | S–M | same protocol round as #3/#4's decisions would be efficient | **YES** |
| 8 | **TASK-212B** | In-battle Card cost and affordability | P2 | None reachable today: the modified cost can never differ from the base cost | DECISION-REQUIRED (`SIGNALR_PROTOCOL.md` §4) **and** blocked by N-05 and #1 | S–M | #1 + N-05 | **YES** |
| 9 | **TASK-220** (proposed) | Player-facing strings: the four rejection codes (N-06), the passive raw id, the raw set ids, the transport name, the protocol citation; consolidate `describeError` | P2 | A rejected cast, a failed start and a connection loss are explained in machine language | NONE for the codes/ids/transport; the `String(error)` fallback needs a recorded choice | S–M | none | partly |
| 10 | **A-12 task** | `CollectionViewerScene` async run guard + in-flight `RETRY` guard | P2 | A slow/failed collection read paints into a torn-down scene | NONE (TASK-205 pattern already governs it) | S | fold with #9 | no |
| 11 | **A-14 fix** | `USERNAME_ALREADY_TAKEN` → `USERNAME_ALREADY_EXISTS` | P2 | One of the most likely first-run errors is untranslated; reported by two audits with no owner | NONE | S | fold with #9 or stand alone | no |
| 12 | **A-15 task** | Logout / 401 session reset / post-login blank bounce | P2 | An expired session is unrecoverable without a reload | NONE on the API (ADR-020 already documents sign-out) | M | none | no |
| 13 | **TASK-214** | Account progression read surface | P2 | Level/XP invisible outside a finished battle; a fresh account shows `—` | DECISION-REQUIRED (a new read, or record Battle History as its home) | M | none | **YES** |
| 14 | **A-16 task** | Battle abandonment | P2 | A genuinely stranded player when no outcome arrives | DECISION-REQUIRED (the outcome set is closed) — **reported as a stop condition**, not designed | S–M | none | **YES** |
| 15 | **TASK-217B** | Cross-reference, envelope and stale-claim sweep (+ N-01, N-04) | P3 | Indirect | DOCUMENTATION-ONLY | M | none | partly |
| 16 | **A-10 / TASK-216 corrected** | Assert the composed reward path; three-write non-atomicity | P2 | Protects the only reward the game grants | NONE expected / a small decision on atomicity | S | none | partly |
| 17 | **N-07, N-08, N-09, N-12, A-17, A-19, A-20** | Test/doc/consistency residue | P3 | None directly | NONE / small decisions | S | fold into the tasks above | partly |

### What moved, and why

* **TASK-213 rises from TASK-212's #3 to #1.** The reason TASK-212 placed it
  behind TASK-212A was that the comprehension gap was still open. It is now
  closed and verified live (§3), and TASK-212A-1's own record states the
  consequence: "the loadout is a permutation: comprehension improved, but no
  build decision changed". The remaining blocker on the loadout screen is
  therefore *reachability*, not legibility — and the same decision gates
  TASK-212B's motivation, TASK-219's content breadth and TASK-215's Passive
  question. It is also S-sized and cannot destabilise anything.
* **TASK-218 holds #2** from TASK-212 (and TASK-211) unchanged: still
  implementation-only, still verified solvable, still every battle, still the
  best value-per-risk item on the board. It can run in parallel with #1.
* **TASK-219 is downgraded from P1-functional to P1-conflict / P2-visible**, and
  is re-scoped (split) rather than promoted, because the functional half of its
  original justification is refuted (§6.3).
* **TASK-215 is confirmed and kept**, now with a precise effect-by-effect status
  and the observation that the four *boss* passives are implemented where the
  five pet passives are not.
* **TASK-212B moves from "next" to #8 and is DEFER'd**, with two named blockers
  instead of the one TASK-212 recorded.
* **TASK-217A is kept ahead of 217B**, with two newly found stale comments added.
* **Nothing in the prior roadmap is dropped.** TASK-212 → 212A (done) + 212B
  (deferred, still the right task); TASK-213/214/215 keep their substance; TASK-216
  stays corrected; TASK-217 stays split; TASK-218/219 keep their subjects.
* **New this audit:** N-11 (the uncommitted chain) is added as a process
  precondition, and the decision-free slice of the cost concern is separated out
  of TASK-212B as #9.

---

## 11. Recommended Immediate Next Task

```text
RECOMMENDED IMMEDIATE NEXT TASK:

TASK-213 — Content reachability: 5 Pets / 5 Pet Skill Cards / ~10 Relics
           vs starter-only ownership
           (a DECISION + authoritative-amendment task; no gameplay
            implementation is authorized by it)
```

**Why this one, on the current evidence.**

1. **It is the only task whose answer changes what every other task may
   implement.** Until it is answered: TASK-212B has no reachable motivation
   (§4.1); TASK-219's read-source decision has no stable scope (how much
   Signature Skill content matters depends on how many Pets can ever be owned);
   TASK-215's magnitude question is adjacent; and the Collection surface, the
   Lobby's 8-row cap (A-18(i)) and any progression work all wait on it.
2. **Its conflict is a live `AGENTS.md` §4 stop condition, not a backlog item.**
   `MVP_SCOPE.md:49-76` lists the content as IN; `MVP_SCOPE.md:148` makes
   unlisted things FUTURE by default; `DATABASE.md:1320-1327` classifies the
   starter grant as bootstrap and explicitly declines to define acquisition; and
   nothing in the nine-route API surface (`API_CONTRACTS.md:146-162`) or in any
   repository method (§7.3) can create an ownership row after Player creation.
   Three documents, one unanswered question.
3. **The evidence is already assembled and the task is cheap.** The reachability
   matrix is §7 above; the decision is S-sized and documentation-only. No code
   change is required to answer it, so it cannot destabilise the tree.
4. **It restores the payoff of work just completed.** TASK-212A-1 made the
   content legible; TASK-213 determines whether legibility can ever change a
   decision. Leaving it unanswered parks the value of the previous task.
5. **It is not being chosen because the roadmap says so.** TASK-212 ranked it
   third; this audit ranks it first *because* TASK-212A-1 landed and the reason
   for the previous ordering no longer exists.

**In parallel, and the answer to "what implementation task next":**

```text
TASK-218 — Name the Pet's Relic triggers in battle
           implementation-only, S, no contract decision, every battle,
           verified solvable against the read the client already holds.
```

If a single *implementation* task must be named instead of a decision task,
it is **TASK-218**; it is independent of TASK-213 and should not wait for it.

**Explicitly not next:** TASK-212B (DEFER, §4). TASK-219 is third and needs the
contract-owner decision that §6.1 names.

---

## 12. Evidence / Verification

### 12.1 Commands executed by this audit

```text
git status --short / --porcelain           37 modified tracked, 11 untracked
git log --oneline -1                       afe5b14
git diff --check                           PASS (exit 0)
npx tsc --noEmit            (client)       PASS (exit 0, no output)
npx vitest run --reporter=basic (client)   PASS — 21 files / 857 tests
dotnet run --project src/backend/GameServer.Api/GameServer.Api.csproj
                                           (Development, dev-auth enabled;
                                            /health -> "Healthy" at :5000)
npm run dev                 (client)       200 at :5173
npm run verify:e2e:smoke                   PASS — 138 checks × 2 runs, 0 failures
Get-NetTCPConnection / port probes         stack up before, stopped after
```

### 12.2 Live E2E evidence this audit relies on (its own run)

```text
phase4.starterGrantLoaded                      {"pets":1,"cards":3,"relics":3}
phase4.fiveBossesRendered                      five Boss options
phase4.cardRowsStateWhatTheyChange             ["Heal 20% Max HP","Power 25","Shield 20% Max HP"]
phase4.relicRowsStateTriggerConditionAndEffect three trigger+condition+effect lines
phase4.contentIsNotAClientSideCatalog          no undelivered content identity on screen
phase4.renderedContentMatchesTheDeliveredResponse  every delivered element rendered
phase4.loadoutSelected                         {"petId":…,"cardsCount":3,"relicsCount":3}
phase5b.passivePresentationIsReadable          "Passive: passive-xich-lang", "0 / 5 Matches (reset: Default)"
phase6.castFeedbackReceived                    RUN 1 "CardCast card-heal: rejected (INSUFFICIENT_POWER)."
                                               RUN 2 "CardCast card-heal: accepted. …"
phase6b.secondCastAcknowledged                 RUN 1 "CardCast card-power-charge: accepted. …"
                                               RUN 2 "CardCast card-power-charge: rejected
                                                      (CARD_CAST_ALREADY_USED_THIS_TURN)."
                                               -> two of the four documented codes observed live
phase6d.signatureSkillControlRemainsUsable     BOTH RUNS:
                                               "CardCast card-inferno: rejected (INSUFFICIENT_POWER)."
phase6c.resyncCarriedHpAndPowerAsState_NoReplay
phase8.preservedLoadoutRestored / IsEditable   the same 3-card / 3-relic permutation
safety.zeroUncaughtExceptions / zeroFatalConsoleErrors /
  zeroUnexpectedApiResponses                   []
```

`phase6d`'s message — reproduced identically in **both** runs — is the runtime
proof of §6.2, and the fact that the check **passed** with that message is the
runtime proof of N-03.

### 12.3 Source areas inspected

```text
Frontend   BattleScene.ts (cast controls, callout, HUD, passive/status rendering,
             cast status, card registry), BattleEventPresenter.ts,
             ContentEffectFormat.ts (new), CollectionModels.ts, GameRuntime.ts,
             GameRuntimeEvents.ts, SignalRService.ts, LobbyScene.ts,
             CollectionViewerScene.ts, ResultScene.ts, PreservedLoadout.ts,
             standalone-web-smoke.mjs
Backend    CardCastExecutor.cs, EffectiveCardCost.cs, CardCostModifier(s).cs,
             CardDefinition.cs, CardCategory.cs, CardCastRejectionReason.cs,
             CardLoadoutService.cs, RelicLoadoutService.cs, RelicResolver.cs,
             RelicDefinition.cs, RelicFiringPoint.cs, PassiveTracker.cs,
             BattleStateService.cs, BattleStartService.cs, BossDefinitions.cs,
             PlayerStarterGrantFactory.cs, PlayerRepository.cs, PetRepository.cs,
             CardRepository.cs, RelicRepository.cs, ICardRepository.cs,
             IPetRepository.cs, IRelicRepository.cs, CollectionQueryService.cs,
             CardCollectionItem.cs, RelicCollectionItem.cs, CollectionResponses.cs,
             CollectionController.cs, PetCollectionItem.cs, AuthController.cs,
             BattleHub.cs
Migrations 20260926151112, 20260929152651, 20261001112446, 20261002090000,
             20261003074309, 20261004055006, 20261004094100, 20261004153916
Docs       GDD.md, MVP_SCOPE.md, ROADMAP.md, CARD_RULES.md, PASSIVE_RULES.md,
             COMBAT_RULES.md, PET_RULES.md, RELIC_RULES.md, BOSS_RULES.md,
             API_CONTRACTS.md, SIGNALR_PROTOCOL.md, GAME_STATE.md, DATABASE.md,
             ARCHITECTURE.md
Tests      SceneLifecycle.test.ts, LobbyScene.test.ts, CollectionService.test.ts,
             CollectionViewerScene.test.ts, ContentEffectFormat.test.ts,
             BattleEventPresentation.test.ts, RuntimeBoundaries.test.ts,
             GameRuntime.test.ts, SignalRService.test.ts, ResultScene.test.ts,
             CardCastExecutorTests.cs, CardCostRelicIntegrationTests.cs,
             PassiveTrackerTests.cs, PassiveEventSourceTests.cs,
             CardCastTurnAllowanceTests.cs, CardCastExecutorTests event-order test
Records    TASK-207, TASK-211 (implementation + audit), TASK-212, TASK-212A,
             TASK-212A-1, TASK-191, TASK-194, TASK-196, TASK-198, TASK-200,
             TASK-166, TASK-167, TASK-168, TASK-169
```

### 12.4 Limitations

1. The E2E exercises the **Vite dev server**, so `import.meta.env.DEV` is true and
   the two DEV-gated overlays are present; the game's own presentation is measured
   as free of them (`phase5b.developedStatusPanelDoesNotObscureTheHud`,
   `hudDoesNotOverlapTheBoard`).
2. The E2E's battles use a mechanical swap policy, so its VICTORY outcomes are not
   balance evidence and are not used as such.
3. No live database was inspected, so a hand-inserted ownership row cannot be
   excluded — recorded as `UNVERIFIED` in §4.1 and §7.3 rather than assumed away.
4. **The cost guard (N-08) was not extended to the scenes by this audit**, because
   the guard scans file *text* for cost terms and `BattleScene.ts` legitimately
   contains cost vocabulary in its own explanatory comments
   (`:673-674,1074,1901,2012`). Widening it is a real change requiring care, and
   this task must not modify tests.
5. TASK-213…TASK-219 remain **proposals**. Nothing in this audit authorizes them.

---

## 13. Explicit Out-of-Scope Items

This audit did **not**, and the diff proves it did not:

```text
modify production code                 NOT DONE  (0 files under src/ changed)
modify tests                           NOT DONE  (0 files under tests/ changed)
modify product/technical documentation  NOT DONE  (0 files under docs/ changed)
implement TASK-212B                     NOT DONE  (no cost member, no protocol change)
implement Passive mechanics             NOT DONE
implement Signature Skill               NOT DONE
implement acquisition/progression       NOT DONE
redesign Card/Relic presentation        NOT DONE
add CardCostModifiers                   NOT DONE
add new SignalR members                 NOT DONE
change database schema                  NOT DONE
fix TASK-217 documentation drift        NOT DONE  (reported; §8 N-01/N-04/N-10)
fix the divergent TestProvisionedContent fixture   NOT DONE (A-19/D2 carried)
fix unrelated stale task records        NOT DONE  (N-12 reported, not repaired)
refactor architecture                   NOT DONE
commit the working tree                 NOT DONE  (N-11 reports the precondition)
```

Additional deliberate omissions, recorded so they are not read as oversights:

* **A-14 (`USERNAME_ALREADY_TAKEN`) was not fixed** although it is a one-line bug
  reported by two audits — `AGENTS.md` §16 forbids fixing an unrelated defect
  inline in an audit.
* **N-02/N-03 (the masking tests and the method-agnostic E2E check) were not
  changed.** Editing them was explicitly forbidden by the task, and doing so
  before §6.1 is decided would freeze a guessed contract.
* **TASK-212 A-01/A-02 were not edited.** Where this audit disagrees with a
  completed record, it reports the disagreement (§6.3) and leaves the record
  intact (`tasks/TASK_LIFECYCLE.md` §3).

---

```text
TASK-213 AUDIT COMPLETE

Decision: DEFER
          TASK-212B is the right task at the wrong time.
          Blocking conditions (both must clear first):
            (1) TASK-213 — content reachability: the only CardCost source
                (Emergency Core) is unobtainable, so CardCostModifiers[] is
                empty in every battle a real account can start;
            (2) N-05 — the intra-document conflict in SIGNALR_PROTOCOL.md
                (§3.2.20 item 2 vs §4 item 15 / §4.3 item 15) must be
                ratified by the protocol owner.
          Outranked in the next slot by TASK-213 and TASK-218.

Recommended next task: TASK-213
          (content reachability decision; decision + authoritative amendment,
           no gameplay implementation)
          Next implementation task, parallelizable: TASK-218
          (Relic trigger callout; implementation-only, no contract change)

          Also resolved by this audit:
            TASK-212A-1 post-condition: HOLDS (re-verified live)
            Pet Passive: D — deferred by explicit Product Owner decision
              (TASK-200 Q-11) for the EFFECT layer; mechanism and
              presentation implemented; all five effects absent
            Signature Skill: CONTRACT DRIFT — SIGNALR_PROTOCOL.md §4.3
              item 13 requires a `Category == PetSkill` datum from
              API_CONTRACTS.md §5.3, which by contract can never carry one;
              the CardCast dispatch itself is contract-conformant
              (TASK-212 A-01/A-02 partially refuted)
            New findings: N-01 … N-12

Implementation: NOT DONE
Production changes: NONE
Test changes: NONE
Documentation changes: NONE
Records edited: NONE
```
